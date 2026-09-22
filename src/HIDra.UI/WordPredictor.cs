using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Text;

namespace HIDra.UI
{
    /// <summary>
    /// Word suggestions from the prediction engine built into Windows - the one its own
    /// touch keyboard uses. Nothing is installed or bundled, which matters on college
    /// machines where installing anything is not an option.
    ///
    /// That engine is tuned for touchscreens, so it also offers corrections for keys it
    /// thinks were missed ("pre" suggests "odd"). The highlight keyboard cannot mistype,
    /// so only words that genuinely continue what was typed are kept.
    ///
    /// Microsoft marks the API experimental. Any failure leaves the suggestions empty
    /// rather than disturbing typing.
    /// </summary>
    public sealed class WordPredictor
    {
        private readonly TextPredictionGenerator? _generator;

        public WordPredictor()
        {
            try
            {
                // UK English to match the keyboard's UK layout
                var generator = new TextPredictionGenerator("en-GB");
                if (generator.LanguageAvailableButNotInstalled)
                {
                    generator = new TextPredictionGenerator("en-US");
                }

                _generator = generator;
            }
            catch
            {
                _generator = null;
            }
        }

        /// <summary>
        /// Suggest words that complete <paramref name="partialWord"/>, or that are likely
        /// to come next when nothing of the current word has been typed yet.
        /// </summary>
        public async Task<IReadOnlyList<string>> PredictAsync(
            string partialWord, IReadOnlyList<string> previousWords, int count)
        {
            if (_generator == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                IEnumerable<string> candidates;

                if (partialWord.Length == 0)
                {
                    if (previousWords.Count == 0)
                    {
                        return Array.Empty<string>();
                    }

                    candidates = await _generator.GetNextWordCandidatesAsync((uint)count * 2, previousWords);
                }
                else
                {
                    var raw = await _generator.GetCandidatesAsync(
                        partialWord,
                        25,
                        TextPredictionOptions.Predictions | TextPredictionOptions.Corrections,
                        previousWords);

                    candidates = raw.Where(w =>
                        w.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase) &&
                        w.Length > partialWord.Length);
                }

                // Chat habits in the engine offer "x" and "u" as words. A single letter
                // is never worth a trip up to the suggestions, except the two real words.
                return candidates
                    .Where(w => (w.Length > 1 || w is "a" or "I") && !w.Contains(' '))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(count)
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
