using NUnit.Framework;
using System;
using UmbrellaFrame.ModelSync.SQLite;

[TestFixture]
public class SQLiteColumnTypeAttributeTests
{
    [TestCase(SQLiteColumnType.INTEGER)]
    [TestCase(SQLiteColumnType.REAL)]
    [TestCase(SQLiteColumnType.TEXT)]
    [TestCase(SQLiteColumnType.BLOB)]
    [TestCase(SQLiteColumnType.NUMERIC)]
    public void Should_Accept_Types_Without_Length(SQLiteColumnType type)
    {
        var attribute = new SQLiteColumnTypeAttribute(type);
        Assert.AreEqual(type.ToString(), attribute.ColumnType.ToString());
    }
}
