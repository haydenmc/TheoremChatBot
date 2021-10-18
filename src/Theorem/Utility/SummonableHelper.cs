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
        public static string GetSummonVerb(this Type t)
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
        public static string GetSummonVerb(this ISummonable o)
        {
            return o.GetType().GetSummonVerb();
        }
    }
}