using NUnit.Framework;
using System;
using UmbrellaFrame.ModelSync.SqlServer;

[TestFixture]
public class SqlServerColumnTypeAttributeTests
{
    [TestCase(SqlServerColumnType.TINYINT)]
    [TestCase(SqlServerColumnType.SMALLINT)]
    [TestCase(SqlServerColumnType.INT)]
    [TestCase(SqlServerColumnType.BIGINT)]
    [TestCase(SqlServerColumnType.MONEY)]
    [TestCase(SqlServerColumnType.SMALLMONEY)]
    [TestCase(SqlServerColumnType.UNIQUEIDENTIFIER)]
    [TestCase(SqlServerColumnType.XML)]
    public void Should_Accept_Types_Without_Length(SqlServerColumnType type)
    {
        var attribute = new SqlServerColumnTypeAttribute(type);
        Assert.AreEqual(type.ToString(), attribute.ColumnType.ToString());
    }

    [TestCase(SqlServerColumnType.VARCHAR, "255")]
    [TestCase(SqlServerColumnType.NVARCHAR, "1000")]
    [TestCase(SqlServerColumnType.CHAR, "10")]
    [TestCase(SqlServerColumnType.NCHAR, "20")]
    public void Should_Accept_Types_With_Valid_Length(SqlServerColumnType type, string length)
    {
        var attribute = new SqlServerColumnTypeAttribute(type, length);
        Assert.AreEqual(length, attribute.Length);
    }

    [TestCase(SqlServerColumnType.VARCHAR, "100000")]
    [TestCase(SqlServerColumnType.NVARCHAR, "200000")]
    public void Should_Throw_Exception_For_Invalid_Length(SqlServerColumnType type, string length)
    {
        Assert.Throws<ArgumentException>(() => new SqlServerColumnTypeAttribute(type, length));
    }
}
