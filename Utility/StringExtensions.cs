using System.Text.RegularExpressions;
using System.Collections;
using System.Globalization;
using System.ComponentModel;

namespace Theorem.Utility
{
    public static class StringExtensions
    {
        /// <summary>
        /// Enables string formatting by named properties in injection object
        /// </summary>
<<<<<<< HEAD
        public static string Inject(this string formatString, object injectionObject)
=======
		public static string Inject(this string formatString, object injectionObject)
>>>>>>> address some PR comments
        {
            return formatString.Inject(GetPropertyHash(injectionObject));
        }

        /// <summary>
        /// Enables string formatting by values in a dictionary
        /// </summary>
        public static string Inject(this string formatString, IDictionary dictionary)
        {
            return formatString.Inject(new Hashtable(dictionary));
        }

        /// <summary>
        /// Enables string formatting by hashtable
        /// </summary>
        public static string Inject(this string formatString, Hashtable attributes)
        {
            string result = formatString;
            if (attributes == null || formatString == null)
<<<<<<< HEAD
            {
                return result;
            }
=======
                return result;
>>>>>>> address some PR comments

            foreach (string attributeKey in attributes.Keys)
            {
                result = result.InjectSingleValue(attributeKey, attributes[attributeKey]);
            }
            return result;
        }

        /// <summary>
        /// Inject single named value into format string
        /// </summary>
        public static string InjectSingleValue(this string formatString, string key, object replacementValue)
        {
            string result = formatString;
<<<<<<< HEAD
            // regex replacement of key with value, where the generic key format is:
            // Regex foo = new Regex("{(foo)(?:}|(?::(.[^}]*)}))");
            Regex attributeRegex = new Regex("{(" + key + ")(?:}|(?::(.[^}]*)}))");  
            // for key = foo, matches {foo} and {foo:SomeFormat}

            // loop through matches, since each key may be used more than once 
            // (and with a different format string)
=======
            //regex replacement of key with value, where the generic key format is:
            //Regex foo = new Regex("{(foo)(?:}|(?::(.[^}]*)}))");
            Regex attributeRegex = new Regex("{(" + key + ")(?:}|(?::(.[^}]*)}))");  //for key = foo, matches {foo} and {foo:SomeFormat}

            //loop through matches, since each key may be used more than once (and with a different format string)
>>>>>>> address some PR comments
            foreach (Match m in attributeRegex.Matches(formatString))
            {
                string replacement = m.ToString();
                if (m.Groups[2].Length > 0) //matched {foo:SomeFormat}
                {
<<<<<<< HEAD
                    // do a double string.Format - first to build the proper format string, 
                    // and then to format the replacement value
                    string attributeFormatString = string.Format(CultureInfo.InvariantCulture, 
                                                                 "{{0:{0}}}", m.Groups[2]);
                    replacement = string.Format(CultureInfo.CurrentCulture, attributeFormatString, replacementValue);
                }
                else // matched {foo}
                {
                    replacement = (replacementValue ?? string.Empty).ToString();
                }
                // perform replacements, one match at a time
                result = result.Replace(m.ToString(), replacement); 
                // attributeRegex.Replace(result, replacement, 1);
=======
                    //do a double string.Format - first to build the proper format string, and then to format the replacement value
                    string attributeFormatString = string.Format(CultureInfo.InvariantCulture, "{{0:{0}}}", m.Groups[2]);
                    replacement = string.Format(CultureInfo.CurrentCulture, attributeFormatString, replacementValue);
                }
                else //matched {foo}
                {
                    replacement = (replacementValue ?? string.Empty).ToString();
                }
                //perform replacements, one match at a time
                result = result.Replace(m.ToString(), replacement);  //attributeRegex.Replace(result, replacement, 1);
>>>>>>> address some PR comments
            }
            return result;

        }


        /// <summary>
        /// Creates a HashTable based on current object state.
        /// <remarks>Copied from the MVCToolkit HtmlExtensionUtility class</remarks>
        /// </summary>
        private static Hashtable GetPropertyHash(object properties)
        {
            Hashtable values = null;
            if (properties != null)
            {
                values = new Hashtable();
                PropertyDescriptorCollection props = TypeDescriptor.GetProperties(properties);
                foreach (PropertyDescriptor prop in props)
                {
                    values.Add(prop.Name, prop.GetValue(properties));
                }
            }
            return values;
        }
    }
}