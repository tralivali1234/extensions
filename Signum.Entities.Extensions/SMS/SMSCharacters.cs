﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Utilities;
using System.ComponentModel;

namespace Signum.Entities.SMS
{
    public static class SMSCharacters
    {
        public static Dictionary<char, int> NormalCharacters = new Dictionary<char, int>();
        public static Dictionary<char, int> DoubleCharacters = new Dictionary<char, int>();

        static SMSCharacters()
        {
            FillNormalCharacaters();
            FillDoubleCharacters();
        }

        private static void FillDoubleCharacters()
        {
            LoadDoublePeriod(91, 94);
            LoadDoublePeriod(123, 126);
            DoubleCharacters.Add('€', (ushort)'€');
        }

        private static void LoadDoublePeriod(int a, int b)
        {
            for (int i = a; i <= b; i++)
            {
                DoubleCharacters.Add(Convert.ToChar(i), i);
            }
        }

        private static void FillNormalCharacaters()
        {
            NormalCharacters.Add(' ', (ushort)' ');
            LoadNormalPeriod(33, 90);
            LoadNormalPeriod(97, 122);

            LoadNormalRange(10, 13, 95);
            LoadNormalRange(161, 163, 165, 167, 191, 201, 209, 214, 216, 220,
            228, 230, 233, 246, 252);

            LoadNormalPeriod(196, 199);

            LoadNormalPeriod(223, 224);
            LoadNormalPeriod(235, 236);
            LoadNormalPeriod(241, 242);
            LoadNormalPeriod(248, 249);
        }

        private static void LoadNormalRange(params int[] caracter)
        {
            foreach (int c in caracter)
            {
                NormalCharacters.Add(Convert.ToChar(c), c);
            }
        }

        private static void LoadNormalPeriod(int a, int b)
        {
            for (int i = a; i <= b; i++)
            {
                NormalCharacters.Add(Convert.ToChar(i), i);
            }
        }

        public const int SMSMaxTextLength = 160; //default length for SMS messages
        public const int TripleSMSMaxTextLength = 160 * 3;

        public static int RemainingLength(string text, int maxLength)
        {
            if (maxLength == 0)
                maxLength = SMSMaxTextLength;
            int count = text.Length;
            foreach (var l in text.ToCharArray())
            {
                if (!SMSCharacters.NormalCharacters.ContainsKey(l))
                {
                    if (SMSCharacters.DoubleCharacters.ContainsKey(l))
                        count += 1;
                    else
                    {
                        maxLength = 60;
                        count = text.Length;
                        break;
                    }
                }
            }
            return maxLength - count;
        }

        public static int RemainingLength(string text)
        {
            return RemainingLength(text, 0);
        }

        public static string RemoveNoSMSCharacters(string text)
        {
            if (text == null)
                return null;
            StringBuilder sb = new StringBuilder();
            foreach (var c in text.RemoveDiacritics().ToCharArray())
	        {
                if (NormalCharacters.ContainsKey(c) || DoubleCharacters.ContainsKey(c))
                    sb.Append(c);
	        }
            return sb.ToString();
        }
    }

    public enum SMSCharactersMessage
    {
        Insert,
        Message,
        RemainingCharacters,
        RemoveNonValidCharacters,
        StatusCanNotBeUpdatedForNonSentMessages,
        [Description("The template must be Active to construct SMS messages")]
        TheTemplateMustBeActiveToConstructSMSMessages,
        TheTextForTheSMSMessageExceedsTheLengthLimit,
        Language,
        Replacements
    }
}
