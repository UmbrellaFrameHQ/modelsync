using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class ModelSyncPlanBuilderTests
{
    [Test]
    public void Build_ShouldUseGlobalPhasesForTablesIndexesAndForeignKeys()
    {
        var child = Table("Child",
            Column("Id"),
            Column("ParentId", foreignTable: "Parent", referenceColumn: "Id"),
            Column("Code", indexed: true));
        var parent = Table("Parent", Column("Id"));

        var existingChild = new DatabaseTableDefinition { Schema = "app", Name = "Child" };
        existingChild.Columns["Id"] = new DatabaseColumnDefinition { Name = "Id", StoreType = "INT" };
        existingChild.Columns["ParentId"] = new DatabaseColumnDefinition { Name = "ParentId", StoreType = "INT" };
        existingChild.Columns["Code"] = new DatabaseColumnDefinition { Name = "Code", StoreType = "INT" };

        var plans = Builder().Build(
            new[] { child, parent },
            new Dictionary<string, DatabaseTableDefinition>
            {
                [ModelSyncPlanBuilder.Key("app", "Child")] = existingChild
            },
            new ModelSyncOptions()).ToList();

        var createParent = plans.Single(p => p.ChangeType == ModelSyncChangeType.CreateTable && p.Table == "Parent");
        var addIndex = plans.Single(p => p.ChangeType == ModelSyncChangeType.AddIndex && p.Table == "Child");
        var addForeignKey = plans.Single(p => p.ChangeType == ModelSyncChangeType.AddForeignKey && p.Table == "Child");

        Assert.That(createParent.Phase, Is.EqualTo(ModelSyncPlanPhase.CreateTables));
        Assert.That(addIndex.Phase, Is.EqualTo(ModelSyncPlanPhase.AddIndexes));
        Assert.That(addForeignKey.Phase, Is.EqualTo(ModelSyncPlanPhase.AddForeignKeys));
        Assert.That(plans.IndexOf(createParent), Is.LessThan(plans.IndexOf(addForeignKey)));
        Assert.That(plans.IndexOf(addIndex), Is.LessThan(plans.IndexOf(addForeignKey)));
    }

    [Test]
    public void Build_ShouldNotPlanUniqueConstraintWhenSemanticUniqueIndexExistsWithDifferentName()
    {
        var table = Table("Products", Column("Code", unique: true));
        var dbTable = new DatabaseTableDefinition { Schema = "app", Name = "Products" };
        dbTable.Columns["Code"] = new DatabaseColumnDefinition { Name = "Code", StoreType = "INT" };
        dbTable.SemanticIndexes.Add(new DatabaseIndexDefinition
        {
            Name = "UX_ManuallyNamed",
            IsUnique = true,
            Columns = { "Code" }
        });

        var plans = Builder().Build(
            new[] { table },
            new Dictionary<string, DatabaseTableDefinition> { [ModelSyncPlanBuilder.Key("app", "Products")] = dbTable },
            new ModelSyncOptions()).ToList();

        Assert.That(plans.Any(p => p.ChangeType == ModelSyncChangeType.AddUniqueConstraint), Is.False);
    }

    [Test]
    public void Build_ShouldClassifyDisabledSafeOperationsAsSkippedWithoutBlocking()
    {
        var table = Table("Products", Column("Name", indexed: true));
        var dbTable = new DatabaseTableDefinition { Schema = "app", Name = "Products" };
        dbTable.Columns["Name"] = new DatabaseColumnDefinition { Name = "Name", StoreType = "INT" };

        var plans = Builder().Build(
            new[] { table },
            new Dictionary<string, DatabaseTableDefinition> { [ModelSyncPlanBuilder.Key("app", "Products")] = dbTable },
            new ModelSyncOptions { AddMissingIndexes = false }).ToList();

        var result = new ModelSyncResult(plans, (_, _) => System.Threading.Tasks.Task.CompletedTask);

        Assert.That(result.SkippedOperations.Count, Is.EqualTo(1));
        Assert.That(result.SkippedOperations[0].ChangeType, Is.EqualTo(ModelSyncChangeType.AddIndex));
        Assert.That(result.BlockedOperations, Is.Empty);
    }

    [Test]
    public async Task Build_WithManualOnlyTable_ShouldReportManualOperationsWithoutBlockingAutomaticApply()
    {
        var manual = Table("Users", Column("Id"));
        var automatic = Table("AuditLogs", Column("Id"));
        manual.ModelType = typeof(ManualUsersModel);
        var options = new ModelSyncOptions();
        options.TablePolicies
            .ForType(manual.ModelType, ModelSyncTableMode.ManualOnly)
            .ForTable("app", "AuditLogs", ModelSyncTableMode.ApplySafeChanges);

        var plans = Builder().Build(
            new[] { manual, automatic },
            new Dictionary<string, DatabaseTableDefinition>(),
            options);
        var applied = new List<string>();
        var result = new ModelSyncResult(plans, (sql, _) =>
        {
            applied.Add(sql);
            return Task.CompletedTask;
        });

        Assert.That(result.ManualOperations.Single().Table, Is.EqualTo("Users"));
        Assert.That(result.AutomaticOperations.Single().Table, Is.EqualTo("AuditLogs"));
        Assert.That(result.BlockedOperations, Is.Empty);

        await result.ApplyAsync();

        Assert.That(applied, Is.EqualTo(new[] { "CREATE AuditLogs" }));
    }

    [Test]
    public void Build_WithIgnoreTable_ShouldProduceNoNormalDiffOperations()
    {
        var options = new ModelSyncOptions();
        options.TablePolicies.ForTable("app", "LegacyOrders", ModelSyncTableMode.Ignore);

        var plans = Builder().Build(
            new[] { Table("LegacyOrders", Column("Id")) },
            new Dictionary<string, DatabaseTableDefinition>(),
            options);

        Assert.That(plans, Is.Empty);
    }

    [Test]
    public void Resolve_ShouldUseTypeThenTableThenSchemaThenDefaultPolicy()
    {
        var options = new ModelSyncOptions { DefaultTableMode = ModelSyncTableMode.ManualOnly };
        options.TablePolicies
            .ForSchema("app", ModelSyncTableMode.Ignore)
            .ForTable("app", "Orders", ModelSyncTableMode.ApplySafeChanges)
            .ForType<ManualUsersModel>(ModelSyncTableMode.ManualOnly);
        var resolver = new ModelSyncTablePolicyResolver(options);

        Assert.That(resolver.Resolve(new ModelTableDefinition { ModelType = typeof(ManualUsersModel), Schema = "app", Name = "Users" }), Is.EqualTo(ModelSyncTableMode.ManualOnly));
        Assert.That(resolver.Resolve(new ModelTableDefinition { Schema = "app", Name = "Orders" }), Is.EqualTo(ModelSyncTableMode.ApplySafeChanges));
        Assert.That(resolver.Resolve(new ModelTableDefinition { Schema = "app", Name = "Products" }), Is.EqualTo(ModelSyncTableMode.Ignore));
        Assert.That(resolver.Resolve(new ModelTableDefinition { Schema = "sales", Name = "Invoices" }), Is.EqualTo(ModelSyncTableMode.ManualOnly));
    }

    [Test]
    public void Policies_WithConflictingEqualSpecificity_ShouldFailFast()
    {
        var policies = new ModelSyncTablePolicyCollection();
        policies.ForTable("app", "Orders", ModelSyncTableMode.ManualOnly);

        Assert.Throws<System.InvalidOperationException>(() =>
            policies.ForTable("APP", "orders", ModelSyncTableMode.ApplySafeChanges));
    }

    [Test]
    public void Build_WithMissingManualParent_ShouldBlockAutomaticForeignKeyOnly()
    {
        var parent = Table("Customers", Column("Id"));
        var child = Table("Notifications",
            Column("Id"),
            Column("CustomerId", foreignTable: "Customers", referenceColumn: "Id"));
        var options = new ModelSyncOptions();
        options.TablePolicies
            .ForTable("app", "Customers", ModelSyncTableMode.ManualOnly)
            .ForTable("app", "Notifications", ModelSyncTableMode.ApplySafeChanges);

        var plans = Builder().Build(
            new[] { parent, child },
            new Dictionary<string, DatabaseTableDefinition>(),
            options);
        var result = new ModelSyncResult(plans, (_, _) => Task.CompletedTask);

        Assert.That(result.ManualOperations.Any(o => o.ChangeType == ModelSyncChangeType.CreateTable && o.Table == "Customers"), Is.True);
        Assert.That(result.AutomaticOperations.Any(o => o.ChangeType == ModelSyncChangeType.CreateTable && o.Table == "Notifications"), Is.True);
        var blockedFk = result.BlockedOperations.Single(o => o.ChangeType == ModelSyncChangeType.AddForeignKey);
        Assert.That(blockedFk.Reason, Is.EqualTo("Required manual dependency 'app.Customers' does not exist."));
    }

    [Test]
    public void Build_WithExistingManualParent_ShouldAllowAutomaticForeignKey()
    {
        var child = Table("Notifications",
            Column("Id"),
            Column("CustomerId", foreignTable: "Customers", referenceColumn: "Id"));
        var existingParent = new DatabaseTableDefinition { Schema = "app", Name = "Customers" };
        var existingChild = new DatabaseTableDefinition { Schema = "app", Name = "Notifications" };
        existingChild.Columns["Id"] = new DatabaseColumnDefinition { Name = "Id", StoreType = "INT" };
        existingChild.Columns["CustomerId"] = new DatabaseColumnDefinition { Name = "CustomerId", StoreType = "INT" };
        var options = new ModelSyncOptions();
        options.TablePolicies.ForTable("app", "Notifications", ModelSyncTableMode.ApplySafeChanges);

        var plans = Builder().Build(
            new[] { child },
            new Dictionary<string, DatabaseTableDefinition>
            {
                [ModelSyncPlanBuilder.Key("app", "Customers")] = existingParent,
                [ModelSyncPlanBuilder.Key("app", "Notifications")] = existingChild
            },
            options);
        var result = new ModelSyncResult(plans, (_, _) => Task.CompletedTask);

        Assert.That(result.AutomaticOperations.Single(o => o.ChangeType == ModelSyncChangeType.AddForeignKey).Table, Is.EqualTo("Notifications"));
        Assert.That(result.BlockedOperations, Is.Empty);
    }

    [Test]
    public void Build_WithManualChildAndAutomaticParent_ShouldKeepChildOperationsManual()
    {
        var parent = Table("Customers", Column("Id"));
        var child = Table("Notifications",
            Column("Id"),
            Column("CustomerId", foreignTable: "Customers", referenceColumn: "Id"));
        var options = new ModelSyncOptions();
        options.TablePolicies
            .ForTable("app", "Customers", ModelSyncTableMode.ApplySafeChanges)
            .ForTable("app", "Notifications", ModelSyncTableMode.ManualOnly);

        var plans = Builder().Build(
            new[] { parent, child },
            new Dictionary<string, DatabaseTableDefinition>(),
            options);
        var result = new ModelSyncResult(plans, (_, _) => Task.CompletedTask);

        Assert.That(result.AutomaticOperations.Any(o => o.ChangeType == ModelSyncChangeType.CreateTable && o.Table == "Customers"), Is.True);
        Assert.That(result.ManualOperations.Any(o => o.Table == "Notifications"), Is.True);
        Assert.That(result.BlockedOperations, Is.Empty);
    }

    private static ModelSyncPlanBuilder Builder()
        => new ModelSyncPlanBuilder(
            identifier => identifier,
            (schema, table) => string.IsNullOrEmpty(schema) ? table : schema + "." + table,
            table => "CREATE " + table.Name,
            (table, column) => "ADD " + column.Name,
            (table, column) => "DEFAULT " + column.Name,
            (table, column) => "CHECK " + column.Name,
            (table, column) => "UNIQUE " + column.Name,
            (table, column) => "FK " + column.Name,
            (table, column) => "INDEX " + column.Name);

    private static ModelTableDefinition Table(string name, params ModelColumnDefinition[] columns)
    {
        var table = new ModelTableDefinition { Schema = "app", Name = name };
        foreach (var column in columns)
            table.Columns.Add(column);
        return table;
    }

    private static ModelColumnDefinition Column(string name, bool indexed = false, bool unique = false, string foreignTable = "", string referenceColumn = "")
        => new ModelColumnDefinition
        {
            Name = name,
            StoreType = "INT",
            IsIndexed = indexed,
            IsUnique = unique,
            ForeignKeyTable = foreignTable,
            ForeignKeyReferenceColumn = referenceColumn
        };

    private sealed class ManualUsersModel
    {
    }
}
