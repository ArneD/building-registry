namespace BuildingRegistry.Projections.Legacy.BuildingSyndicationWithCount
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Linq;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using NodaTime;

    public static class XmlTools
    {
        private static readonly Type[] WriteTypes =
        {
            typeof(string),
            typeof(DateTime),
            typeof(Enum),
            typeof(decimal),
            typeof(Guid),
            typeof(Instant),
            typeof(LocalDate),
            typeof(LocalDateTime),
            typeof(DateTimeOffset),
            typeof(Organisation)
        };

        /// <summary>
        /// Preferred way to exclude properties
        /// </summary>
        private static readonly Type[] ExcludeTypes =
        {
            typeof(Application),
            typeof(Modification)
        };

        /// <summary>
        /// Alternative way if property is a primitive type or included in WriteTypes.
        /// </summary>
        private static readonly string[] ExcludePropertyNames =
        {
            "Operator"
        };

        private static bool IsSimpleType(this Type type) => type.IsPrimitive || WriteTypes.Contains(type) || WriteTypes.Contains(type.BaseType);

        private static bool IsExcludedType(this Type type) => ExcludeTypes.Contains(type);

        private static bool IsExcludedPropertyName(this string propertyName) => ExcludePropertyNames.Contains(propertyName);

        public static XElement? ToXml(this object input) => input.ToXml(null);

        public static XElement? ToXml(this object? input, string? element, int? arrayIndex = null, string? arrayName = null)
        {
            if (input == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(element))
            {
                var name = input.GetType().Name;

                string GetArrayElement()
                {
                    return arrayIndex != null
                        ? arrayName + "_" + arrayIndex
                        : name;
                }

                element = name.Contains("AnonymousType")
                    ? "Object"
                    : GetArrayElement();
            }

            element = XmlConvert.EncodeName(element);
            var ret = new XElement(element);

            var type = input.GetType();
            var props = type.GetProperties();

            var elements = from prop in props
                let pType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType
                let name = XmlConvert.EncodeName(prop.Name)
                let val = pType.IsArray ? "array" : prop.GetValue(input, null)
                let value = pType.IsEnumerable()
                    ? GetEnumerableElements(prop, (IEnumerable)prop.GetValue(input, null)!)
                    : GetElement(pType, name, val)
                where value != null && !pType.IsExcludedType() && !name.IsExcludedPropertyName()
                select value;

            XElement? GetElement(Type pType, string s, object o)
                => pType.IsSimpleType() ? new XElement(s, GetValue(o)) : o.ToXml(s);

            ret.Add(elements);

            return ret;
        }

        private static object? GetValue(object? val)
        {
            return val switch
            {
                LocalDate localDate => localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LocalDateTime localDateTime => localDateTime.ToString("o", CultureInfo.InvariantCulture),
                DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("o", CultureInfo.InvariantCulture),
                _ => val
            };
        }

        private static readonly Type[] FlatternTypes =
        {
            typeof(string)
        };

        public static bool IsEnumerable(this Type type) => typeof(IEnumerable).IsAssignableFrom(type) && !FlatternTypes.Contains(type);

        private static XElement GetEnumerableElements(PropertyInfo info, IEnumerable input)
        {
            var name = XmlConvert.EncodeName(info.Name);

            var rootElement = new XElement(name);

            var i = 0;
            foreach (var v in input)
            {
                var childElement = v.GetType().IsSimpleType() || v.GetType().IsEnum ? new XElement(name + "_" + i, v) : ToXml(v, null, i, name);
                rootElement.Add(childElement);
                i++;
            }

            return rootElement;
        }
    }
}
