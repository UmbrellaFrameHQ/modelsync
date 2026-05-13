using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UmbrellaFrame.ModelSync.Core.Exceptions;
using UmbrellaFrame.ModelSync.Core.Resources;

namespace UmbrellaFrame.ModelSync.Core.Helpers
{
    public class DynamicPropertyManager<T> where T : class, new()
    {
        private readonly Lazy<ConcurrentDictionary<string, PropertyMetadata>> _properties;

        public DynamicPropertyManager()
        {
            _properties = new Lazy<ConcurrentDictionary<string, PropertyMetadata>>(LoadProperties);
        }

        private ConcurrentDictionary<string, PropertyMetadata> LoadProperties()
        {
            var properties = new ConcurrentDictionary<string, PropertyMetadata>();

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                properties[prop.Name] = new PropertyMetadata
                {
                    PropertyInfo = prop,
                    Attributes = prop.GetCustomAttributes().ToList()
                };
            }

            return properties;
        }

        public void LoadFromModel(T model)
        {
            foreach (var prop in _properties.Value)
            {
                prop.Value.Value = prop.Value.PropertyInfo.GetValue(model);
            }
        }

        public void AddOrUpdateProperty(string propertyName, object value)
        {
            SetProperty(propertyName, value);
        }

        public object GetProperty(string propertyName)
        {
            if (_properties.Value.TryGetValue(propertyName, out var metadata))
            {
                return metadata.Value!;
            }
            throw new PropertyNotFoundException(CoreResources.Get("PropertyManager_NotFound", propertyName));
        }

        public List<Attribute> GetAttributes(string propertyName)
        {
            if (_properties.Value.TryGetValue(propertyName, out var metadata))
            {
                return metadata.Attributes;
            }
            throw new PropertyNotFoundException(CoreResources.Get("PropertyManager_NotFound", propertyName));
        }

        public TAttribute GetAttribute<TAttribute>(string propertyName) where TAttribute : Attribute
        {
            if (_properties.Value.TryGetValue(propertyName, out var metadata))
            {
                return metadata.Attributes.OfType<TAttribute>().FirstOrDefault();
            }
            throw new PropertyNotFoundException(CoreResources.Get("PropertyManager_NotFound", propertyName));
        }

        public List<KeyValuePair<string, object>> GetAllPropertiesAsList()
        {
            return _properties.Value.Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value.Value!)).ToList();
        }

        /// <summary>
        /// Returns all properties in their declaration order (MetadataToken order),
        /// guaranteeing stable column ordering in generated SQL.
        /// </summary>
        public List<KeyValuePair<string, object>> GetAllPropertiesOrdered()
        {
            return _properties.Value
                .OrderBy(kv => kv.Value.PropertyInfo?.MetadataToken)
                .Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value.Value!))
                .ToList();
        }

        private void SetProperty(string propertyName, object value)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException(CoreResources.Get("PropertyManager_NameNullOrWhitespace"), nameof(propertyName));
            }

            if (_properties.Value.ContainsKey(propertyName))
            {
                _properties.Value[propertyName].Value = value;
            }
            else
            {
                _properties.Value[propertyName] = new PropertyMetadata { Value = value };
            }
        }

        public TAttribute GetClassAttribute<TAttribute>() where TAttribute : Attribute
        {
            var type = typeof(T);
            return (TAttribute)Attribute.GetCustomAttribute(type, typeof(TAttribute));
        }
    }
}
