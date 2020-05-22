﻿using System.Text.RegularExpressions;

namespace GeBoCommon
{
    public static class Constants
    {
#if AI
        public const string GameName = "AI Girl";
        public const string MainGameProcessName = "AI-Syoujyo";
        public const string MainGameProcessNameSteam = "AI-Shoujo";
        public const string StudioProcessName = "StudioNEOV2";
        public const string Prefix = "AI";
        public const RegexOptions SupportedRegexCompilationOption = RegexOptions.Compiled;
#elif EC
        public const string GameName = "Emotion Creators";
        public const string MainGameProcessName = "EmotionCreators";
        public const string StudioProcessName = "***NOPE***";
        public const string Prefix = "EC";
        public const RegexOptions SupportedRegexCompilationOption = RegexOptions.None;
#elif HS
        public const string GameName = "Honey Select";
        public const string MainGameProcessName = "HoneySelect_64";
        public const string BattleArenaProcessName = "BattleArena_64";
        public const string StudioProcessName = "StudioNEO_64";
        public const string Prefix = "HS";
        public const RegexOptions SupportedRegexCompilationOption = RegexOptions.None;
#elif KK
        public const string GameName = "Koikatsu";
        public const string MainGameProcessName = "Koikatu";
        public const string MainGameProcessNameSteam = "Koikatsu Party";
        public const string StudioProcessName = "CharaStudio";
        public const string Prefix = "KK";
        public const RegexOptions SupportedRegexCompilationOption = RegexOptions.None;
#endif
    }
}
