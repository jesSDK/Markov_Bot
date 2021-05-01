using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jay_Bot
{
    class MarkovExperimental
    {

        public static Dictionary<string, Dictionary<string, double>> dicEx = new Dictionary<string, Dictionary<string, double>>();
        public static Random rng = new Random();
        public static Dictionary<string, double> probWordEx;
        public static List<string> startWords = new List<string>();

        public static void markovTrainExperimental(string text)
        {
            string[] words = text.Split(' ');
            List<string> pWord = new List<string>();
            for (int i = 0; i < words.Length - 1; i++)
            {
                string curWord = words[i];
                if (curWord.Contains('\u0002'))
                {
                    startWords.Add(curWord);
                }
                string nextWord = words[i + 1];
                bool keyexists = dicEx.ContainsKey(curWord);
                if (!keyexists)
                {
                    dicEx.Add(curWord, new Dictionary<string, double>());
                }

                if (dicEx[curWord].ContainsKey(nextWord))
                {
                    //dicEx[curWord][nextWord]++;//seen word already, increase count. maybe we can just calc probability here? 
                    dicEx[curWord][nextWord] = Probability(dicEx[curWord][nextWord]);
                }
                else
                {
                    dicEx[curWord].Add(nextWord, Math.Sqrt(1));//get dictionary from dic and add entry of the next word and set count to sqrt1
                }

            }
        }


        private static double Probability(double prob)
        {
            prob = Math.Pow(prob, 2) + 1; //square & add 1
            prob = Math.Sqrt(prob); //sqrt again for better choices
            return prob;
        }

        public static string generateEx()
        {
            string startWord = startWords.ElementAt(rng.Next(0, startWords.Count));
            StringBuilder stringBuilder = new StringBuilder(startWord);
            for (int i = 0; i < dicEx.Count; i++)
            {
                double totalweight = 0;
                foreach (double weight in dicEx[startWord].Values)
                {
                    totalweight += weight;
                }
                double randomnumber = rng.Next(0, (int)totalweight);
                foreach (string newword in dicEx[startWord].Keys)
                {
                    if (randomnumber < dicEx[startWord][newword])
                    {
                        startWord = newword;
                        break;
                    }
                    randomnumber = randomnumber - dicEx[startWord][newword];
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
