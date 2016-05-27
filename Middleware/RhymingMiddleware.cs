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

namespace Theorem.Middleware
{
    public class RhymingMiddleware : IMiddleware
    {
        private IConfigurationRoot _configuration { get; set; }
        private SlackProvider _slackProvider { get; set; }
        private const string _rhymeApiBaseUrl = "http://rhymebrain.com/talk";
        private readonly double _percentRhymingProbability = 0.1;
        private readonly double _percentRhymingWordsRequired = 0.5;
        private readonly int _maxWordsToRhyme = 12;
        private const string _wordMatchPattern = @"[a-zA-Z']+";
        
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
            if (message.UserId != _slackProvider.Self.Id && random.NextDouble() < _percentRhymingProbability)
            {
                // Split up sentence into words
                var originalWords = Regex.Matches(message.Text, _wordMatchPattern).Cast<Match>().Select(m => m.Value).ToArray();
                if (originalWords.Length > _maxWordsToRhyme)
                {
                    return MiddlewareResult.Continue;
                }
                
                // Let's see if we can find rhymes
                var rhymes = new string[originalWords.Length];
                var queryString = new StringBuilder("");
                for (var i = 0; i < originalWords.Length; i++)
                {
                    if (i > 0)
                    {
                        queryString.Append("&next&function=getRhymes");
                    }
                    else
                    {
                        queryString.Append("?function=getRhymes");
                    }
                    queryString.Append("&word=").Append(Uri.EscapeDataString(originalWords[i]));
                }
                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri(_rhymeApiBaseUrl);
                    var result = httpClient.GetAsync(queryString.ToString()).Result;
                    var resultStr = result.Content.ReadAsStringAsync().Result;
                    var resultRhymes = JsonConvert.DeserializeObject<List<List<RhymeModel>>>(resultStr);
                    for (var i = 0; i < resultRhymes.Count; i++)
                    {
                        var wordRhymes = resultRhymes[i];
                        var syllables = CountSyllables(originalWords[i]);
                        var rhymingWord = wordRhymes
                            .Where(w => w.Syllables == syllables)
                            .Where(w => w.Score > 200)
                            .OrderByDescending(w => w.Score)
                            .Take(10)
                            .OrderBy(r => random.Next())
                            .FirstOrDefault();
                        if (rhymingWord != null)
                        {
                            rhymes[i] = rhymingWord.Word;
                        }
                        else
                        {
                            rhymes[i] = originalWords[i];
                        }
                    }
                    // TODO: Replace original words with new rhyming words, send message.
                }
                return MiddlewareResult.Continue;
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
            string currentWord = word;
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