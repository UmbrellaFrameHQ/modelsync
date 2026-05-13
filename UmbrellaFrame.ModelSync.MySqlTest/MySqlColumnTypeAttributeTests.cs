
using System;

using NUnit.Framework;

using UmbrellaFrame.ModelSync.MySql;

[TestFixture]
public class MySqlColumnTypeAttributeTests
{
    [TestCase(MySqlColumnType.TINYINT)]
    [TestCase(MySqlColumnType.SMALLINT)]
    [TestCase(MySqlColumnType.MEDIUMINT)]
    [TestCase(MySqlColumnType.INT)]
    [TestCase(MySqlColumnType.BIGINT)]
    [TestCase(MySqlColumnType.JSON)]
    [TestCase(MySqlColumnType.BIT)]
    public void Should_Accept_Types_Without_Length(MySqlColumnType type)
    {
        var attribute = new MySqlColumnTypeAttribute(type);
        Assert.AreEqual(type.ToString(), attribute.ColumnType.ToString());
    }

    [TestCase(MySqlColumnType.VARCHAR, "255")]
    [TestCase(MySqlColumnType.CHAR, "50")]
    [TestCase(MySqlColumnType.TINYTEXT, "255")]
    [TestCase(MySqlColumnType.TEXT, "5000")]
    public void Should_Accept_Types_With_Valid_Length(MySqlColumnType type, string length)
    {
        var attribute = new MySqlColumnTypeAttribute(type, length);
        Assert.AreEqual(length, attribute.Length);
    }

    [TestCase(MySqlColumnType.VARCHAR, "100000")]
    [TestCase(MySqlColumnType.CHAR, "200000")]
    public void Should_Throw_Exception_For_Invalid_Length(MySqlColumnType type, string length)
    {
        Assert.Throws<ArgumentException>(() => new MySqlColumnTypeAttribute(type, length));
    }
}
