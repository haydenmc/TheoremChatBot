using Theorem.Models;

namespace Theorem.Middleware
{
    public interface ISummonable
    {
        string MentionRegex { get; }

        static string SummonVerb { get; }

        string Usage { get; }
    }
}