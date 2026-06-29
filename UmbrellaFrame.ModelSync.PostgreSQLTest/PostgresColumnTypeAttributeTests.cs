using NUnit.Framework;
using System;

using UmbrellaFrame.ModelSync.PostgreSQL;

[TestFixture]
public class PostgresColumnTypeAttributeTests
{
    [TestCase(PostgresColumnType.SMALLINT)]
    [TestCase(PostgresColumnType.INTEGER)]
    [TestCase(PostgresColumnType.BIGINT)]
    [TestCase(PostgresColumnType.UUID)]
    [TestCase(PostgresColumnType.JSON)]
    [TestCase(PostgresColumnType.JSONB)]
    [TestCase(PostgresColumnType.BOOLEAN)]
    public void Should_Accept_Types_Without_Length(PostgresColumnType type)
    {
        var attribute = new PostgresColumnTypeAttribute(type);
        Assert.AreEqual(type.ToString(), attribute.ColumnType.ToString());
    }

    [TestCase(PostgresColumnType.VARCHAR, "255")]
    [TestCase(PostgresColumnType.CHAR, "10")]
    public void Should_Accept_Types_With_Valid_Length(PostgresColumnType type, string length)
    {
        var attribute = new PostgresColumnTypeAttribute(type, length);
        Assert.AreEqual(length, attribute.Length);
    }

    [TestCase(PostgresColumnType.VARCHAR, "70000")]
    public void Should_Throw_Exception_For_Invalid_Length(PostgresColumnType type, string length)
    {
        Assert.Throws<ArgumentException>(() => new PostgresColumnTypeAttribute(type, length));
    }
}
