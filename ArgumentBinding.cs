using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Runtime.Serialization;

namespace top.net;

internal class ArgumentBinding
{
    public static void Bind(IConfiguration configuration, object settings)
    {
        IList<PropertyInfo> props = new List<PropertyInfo>(settings.GetType().GetProperties());

        foreach (PropertyInfo prop in props)
        {
            string name = prop.Name;
            DataMemberAttribute attr = prop.GetCustomAttribute(typeof(DataMemberAttribute)) as DataMemberAttribute;

            if (attr != null)
            {
                name = attr.Name;
            }

            KeyValuePair<string, string> argument = configuration.AsEnumerable().SingleOrDefault(argument => argument.Key.ToLower() == name.ToLower());

            if (argument.Key != null)
            {
                if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(settings, true);
                }
                else
                {
                    prop.SetValue(settings, argument.Value);
                }
            }
        }
    }
}
