# Migration Runner Example

Use migration runners when your project keeps SQL setup files for tables, stored procedures, triggers, and seeds.

```csharp
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(connectionString);

runner.RegisterScriptFile("Database/Scripts/Tables/001_CreateProducts.sql");
runner.RegisterScriptFile("Database/Scripts/StoredProcedures/010_GetProducts.sql");
runner.RegisterScriptFile("Database/Scripts/Triggers/020_ProductAudit.sql");
runner.RegisterScriptFile("Database/Scripts/Seeds/030_DefaultProducts.sql");

var plans = await runner.CompareRegisteredAsync();

foreach (var plan in plans)
{
    Console.WriteLine($"{plan.Definition.Category} {plan.Definition.Id}: {plan.ChangeType}");
}

await runner.RunAsync();
```

Scripts are applied in this order:

```text
Tables -> StoredProcedures -> Triggers -> Seeds
```

SQL Server scripts can contain `GO` batch separators. Database reset is available only with explicit destructive opt-in:

```csharp
var options = new MigrationRunnerOptions
{
    ResetDatabase = true,
    DestructiveOptions = DestructiveOperationOptions.Allow()
};
```
