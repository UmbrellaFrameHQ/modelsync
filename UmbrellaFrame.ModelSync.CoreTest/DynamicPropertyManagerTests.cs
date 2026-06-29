using NUnit.Framework;
using System.Collections.Generic;
using UmbrellaFrame.ModelSync.Core.Helpers;

namespace UmbrellaFrame.ModelSync.CoreTest;

[TestFixture]
public class DynamicPropertyManagerTests
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class PropertyDescriptionAttributeMock : Attribute
    {
        public string Description { get; }

        public PropertyDescriptionAttributeMock(string description)
        {
            Description = description;
        }
    }

    public class MockPersonModelMock
    {
        [PropertyDescriptionAttributeMock("First name of the person")]
        public string FirstName { get; set; }

        [PropertyDescriptionAttributeMock("Last name of the person")]
        public string LastName { get; set; }

        [PropertyDescriptionAttributeMock("Unique identifier for the person")]
        public int Id { get; set; }
    }

    private DynamicPropertyManager<MockPersonModelMock> _propertyManager;
    private MockPersonModelMock _MockPersonModelMock;

    [SetUp]
    public void SetUp()
    {
        _MockPersonModelMock = new MockPersonModelMock
        {
            FirstName = "John",
            LastName = "Doe",
            Id = 1
        };

        _propertyManager = new DynamicPropertyManager<MockPersonModelMock>();
        _propertyManager.LoadFromModel(_MockPersonModelMock);
    }

    [Test]
    public void LoadFromModel_ShouldLoadPropertiesCorrectly()
    {
        Assert.That(_propertyManager.GetProperty("FirstName"), Is.EqualTo("John"));
        Assert.That(_propertyManager.GetProperty("LastName"), Is.EqualTo("Doe"));
        Assert.That(_propertyManager.GetProperty("Id"), Is.EqualTo(1));
    }

    [Test]
    public void GetProperty_ShouldReturnCorrectValue()
    {
        var firstName = _propertyManager.GetProperty("FirstName");
        Assert.That(firstName, Is.EqualTo("John"));
    }

    [Test]
    public void GetAttributes_ShouldReturnAttributesForProperty()
    {
        var firstNameAttributes = _propertyManager.GetAttributes("FirstName");
        Assert.That(firstNameAttributes, Is.Not.Null);
    }

    [Test]
    public void GetAttribute_ShouldReturnSpecificAttribute()
    {
        var descriptionAttribute = _propertyManager.GetAttribute<PropertyDescriptionAttributeMock>("Id");
        Assert.That(descriptionAttribute, Is.Not.Null);
        Assert.That(descriptionAttribute.Description, Is.EqualTo("Unique identifier for the person"));
    }

    [Test]
    public void AddOrUpdateProperty_ShouldUpdateValue()
    {
        _propertyManager.AddOrUpdateProperty("FirstName", "Jane");
        var updatedValue = _propertyManager.GetProperty("FirstName");
        Assert.That(updatedValue, Is.EqualTo("Jane"));
    }

    [Test]
    public void GetAllPropertiesAsList_ShouldReturnAllProperties()
    {
        var properties = _propertyManager.GetAllPropertiesAsList();
        var propertyNames = new HashSet<string>(properties.Select(p => p.Key));

        Assert.That(propertyNames.Contains("FirstName"), Is.True);
        Assert.That(propertyNames.Contains("LastName"), Is.True);
        Assert.That(propertyNames.Contains("Id"), Is.True);
    }
}
