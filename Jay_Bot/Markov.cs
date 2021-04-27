using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jay_Bot
{
    class Markov
    {

        public static Dictionary<string, Dictionary<string, int>> dic = new Dictionary<string, Dictionary<string, int>>();
        public static Random rng = new Random();

        public static void markovTrain(string text)
        {
            string[] words = text.Split(' ');
            List<string> pWord = new List<string>();
            for (int i = 0; i < words.Length - 1; i++)
            {
                string curWord = words[i];
                string nextWord = words[i + 1];
                bool keyexists = dic.ContainsKey(curWord);
                if (!keyexists)
                {
                    dic.Add(curWord, new Dictionary<string, int>());
                }

                if (dic[curWord].ContainsKey(nextWord))
                {
                    dic[curWord][nextWord]++;
                }
                else
                {
                    dic[curWord].Add(nextWord, 1);
                }

            }
        }


        public static string generate()
        {
            List<string> startWords = new List<string>();
            foreach (string sWord in dic.Keys)
            {
                if (sWord.Contains('\u0002'))
                {
                    startWords.Add(sWord);
                }
            }
            string startWord = startWords.ElementAt(rng.Next(0, startWords.Count));
            StringBuilder stringBuilder = new StringBuilder(startWord);
            for (int i = 0; i < dic.Count; i++)
            {
                Dictionary<string, int> assDic;
                Dictionary<string, double> probWord = new Dictionary<string, double>();
                double totalweight = 0;
                assDic = dic[startWord];
                foreach (var assWord in assDic)
                {
                    double nvalue = assWord.Value;
                    probWord.Add(assWord.Key, System.Math.Sqrt(nvalue));
                }
                foreach (double weight in probWord.Values)
                {
                    totalweight += weight;
                }
                double randomnumber = rng.Next(0, (int)totalweight);
                foreach (string newword in probWord.Keys)
                {
                    if (randomnumber < probWord[newword])
                    {
                        startWord = newword;
                        break;
                    }
                    randomnumber = randomnumber - probWord[newword];
                }
                if (startWord.Contains('\u0003'))
                {
                    var loc = startWord.IndexOf("\u0003");
                    string lastword = startWord.Substring(0, loc);
                    stringBuilder.Append(" " + lastword);
                    return stringBuilder.ToString();

                }
                else
                {
                    stringBuilder.Append(" " + startWord);
                }
            }
            return "generation not ready";
        }




    }
}
