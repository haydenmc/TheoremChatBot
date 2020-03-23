using System.Text.RegularExpressions;
using System.Collections;
using System.Globalization;
using System;
using Theorem.Middleware;

namespace Theorem.Utility
{
    public static class SummonableHelper
    {
        /// <summary>
        /// Get static Summon Verb value for ISummonable type
        /// </summary>
<<<<<<< HEAD
        public static string GetSummonVerb(this Type t)
=======
		public static string GetSummonVerb(this Type t)
>>>>>>> address some PR comments
        {
            if (!typeof(ISummonable).IsAssignableFrom(t))
            {
                return string.Empty;
            }

            var prop = t.GetProperty(nameof(ISummonable.SummonVerb));
            return prop?.GetValue(null, null).ToString();
        }

        /// <summary>
        /// Get static Summon Verb value for ISummonable object
        /// </summary>
<<<<<<< HEAD
        public static string GetSummonVerb(this ISummonable o)
=======
		public static string GetSummonVerb(this ISummonable o)
>>>>>>> address some PR comments
        {
            return o.GetType().GetSummonVerb();
        }
    }
}