using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Theorem.Models;
using Theorem.Providers;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Theorem.Models.Events;

namespace Theorem.Middleware
{
    public class RhymingMiddleware : IMiddleware
    {
        private IConfigurationRoot _configuration { get; set; }
        private SlackProvider _slackProvider { get; set; }
        private const string _rhymeApiBaseUrl = "http://rhymebrain.com/talk";
        private readonly double _percentRhymingProbability = 0.02;
        private readonly double _percentRhymingWordsRequired = 0.75;
        private readonly int _maxWordsToRhyme = 6;
        private readonly int _minRhymeScore = 250;
        private const string _wordMatchPattern = @"[a-zA-Z']+";
        
        private class RhymeMatch
        {
            public int Index;
            public string Word;
        }
        
        public RhymingMiddleware(SlackProvider slackProvider, IConfigurationRoot configuration)
        {
            _slackProvider = slackProvider;
            _configuration = configuration;
            if (_configuration["Middleware:Rhyming:PercentRhymingProbability"] != null)
            {
                double.TryParse(_configuration["Middleware:Rhyming:PercentRhymingProbability"], out _percentRhymingProbability);
            }
            if (_configuration["Middleware:Rhyming:PercentRhymingWordsRequired"] != null)
            {
                double.TryParse(_configuration["Middleware:Rhyming:PercentRhymingWordsRequired"], out _percentRhymingWordsRequired);
            }
            if (_configuration["Middleware:Rhyming:MaxWordsToRhyme"] != null)
            {
                int.TryParse(_configuration["Middleware:Rhyming:MaxWordsToRhyme"], out _maxWordsToRhyme);
            }
        }
        
        public MiddlewareResult ProcessMessage(MessageEventModel message)
        {
            Random random = new Random();
            if (message.SlackUserId != _slackProvider.Self.Id && random.NextDouble() < _percentRhymingProbability)
            {
                // Split up sentence into words
                var originalWordMatches = Regex
                    .Matches(message.Text, _wordMatchPattern)
                    .Cast<Match>()
                    .Select(m => new RhymeMatch() { Index = m.Index, Word = m.Value })
                    .ToList();
                if (originalWordMatches.Count > _maxWordsToRhyme)
                {
                    return MiddlewareResult.Continue;
                }
                
                // Generate a query string for the API
                var queryString = new StringBuilder("");
                for (var i = 0; i < originalWordMatches.Count; i++)
                {
                    if (i > 0)
                    {
                        queryString.Append("&next&function=getRhymes");
                    }
                    else
                    {
                        queryString.Append("?function=getRhymes");
                    }
                    queryString.Append("&word=").Append(Uri.EscapeUriString(originalWordMatches[i].Word));
                }
                
                // Run the query
                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri(_rhymeApiBaseUrl);
                    var result = httpClient.GetAsync(queryString.ToString()).Result;
                    var resultStr = result.Content.ReadAsStringAsync().Result;
                    var resultRhymes = JsonConvert.DeserializeObject<List<List<RhymeModel>>>(resultStr);
                    var newMessage = message.Text;
                    int wordsRhymed = 0;
                    for (var i = 0; i < resultRhymes.Count; i++)
                    {
                        var wordRhymes = resultRhymes[i];
                        var syllables = CountSyllables(originalWordMatches[i].Word);
                        var rhymingWord = wordRhymes
                            .Where(w => w.Syllables == syllables)
                            .Where(w => w.Score > _minRhymeScore)
                            .OrderBy(r => Guid.NewGuid())
                            .FirstOrDefault();
                        if (rhymingWord != null)
                        {
                            // Get length delta
                            int lengthDelta = rhymingWord.Word.Length - originalWordMatches[i].Word.Length;
                            // Replace it!
                            newMessage = newMessage
                                .Remove(originalWordMatches[i].Index, originalWordMatches[i].Word.Length)
                                .Insert(originalWordMatches[i].Index, rhymingWord.Word);
                            // Bump indexes of other words
                            for (var j = i + 1; j < originalWordMatches.Count; j++)
                            {
                                originalWordMatches[j].Index += lengthDelta;
                            }
                            wordsRhymed++;
                        }
                    }
                    // Make sure we've successfully rhymed enough words.
                    if (wordsRhymed / (double)originalWordMatches.Count < _percentRhymingWordsRequired)
                    {
                        return MiddlewareResult.Continue;
                    }
                    // Send the result!
                    _slackProvider.SendMessageToChannelId(message.SlackChannelId, newMessage).Wait();
                    return MiddlewareResult.Continue;
                }
            }
            return MiddlewareResult.Continue;
        }
        
        /// <summary>
        /// Guesses number of syllables in a given word
        /// Thanks to Joe Basirico
        /// http://stackoverflow.com/a/5615724/2874534
        /// </summary>
        /// <param name="word">Word</param>
        /// <returns>Syllable count</returns>
        private int CountSyllables(string word)
        {
            char[] vowels = { 'a', 'e', 'i', 'o', 'u', 'y' };
            string currentWord = word.ToLowerInvariant();
            int numVowels = 0;
            bool lastWasVowel = false;
            foreach (char wc in currentWord)
            {
                bool foundVowel = false;
                foreach (char v in vowels)
                {
                    //don't count diphthongs
                    if (v == wc && lastWasVowel)
                    {
                        foundVowel = true;
                        lastWasVowel = true;
                        break;
                    }
                    else if (v == wc && !lastWasVowel)
                    {
                        numVowels++;
                        foundVowel = true;
                        lastWasVowel = true;
                        break;
                    }
                }

                //if full cycle and no vowel found, set lastWasVowel to false;
                if (!foundVowel)
                {
                    lastWasVowel = false;
                }
            }
            //remove es, it's _usually? silent
            if (currentWord.Length > 2 && 
                currentWord.Substring(currentWord.Length - 2) == "es")
            {
                numVowels--;
            }
                
            // remove silent e
            else if (currentWord.Length > 1 &&
                currentWord.Substring(currentWord.Length - 1) == "e")
            {
                numVowels--;
            }   

            return numVowels;
        }
    }
}