﻿using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using BepInEx.Logging;
using GeBoCommon.Utilities;

namespace GameDialogHelperPlugin
{
    public class QuestionInfo
    {
        private static readonly SimpleLazy<QuestionInfo> _default = new SimpleLazy<QuestionInfo>(() => new QuestionInfo
        {
            Id = -1, Description = string.Empty, QuestionType = QuestionType.Unknown
        });

        private static readonly SimpleLazy<Dictionary<int, QuestionInfo>> _questionInfos =
            new SimpleLazy<Dictionary<int, QuestionInfo>>(() =>
            {
                var result = new Dictionary<int, QuestionInfo>();
                foreach (var qi in LoadQuestions())
                {
                    Logger.LogDebug(qi);
                    result[qi.Id] = qi;
                }

                Logger.LogError($"Loaded questions XML: {result.Count}");
                return result;
            });

        private string _description;
        private static ManualLogSource Logger => GameDialogHelper.Logger;

        public static QuestionInfo Default => _default.Value;

        public int Id { get; private set; }

        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(_description))
                {
                    switch (QuestionType)
                    {
                        case QuestionType.Likes:
                            _description = $"Likes {LikeTarget}";
                            break;

                        case QuestionType.PhysicalAttributes:
                            _description = $"{PhysicalAttributeTarget} preference";
                            break;

                        case QuestionType.Invitation:
                            _description = $"Invitation to ${InvitationTarget}";

                            break;

                        default:
                            _description = $"{QuestionType}";
                            break;
                    }

                    if (RelationshipLevel != RelationshipLevel.Anyone)
                    {
                        _description += $" (from {RelationshipLevel})";
                    }
                }

                return _description;
            }

            private set => _description = value;
        }

        public QuestionType QuestionType { get; private set; }

        public RelationshipLevel RelationshipLevel { get; } = RelationshipLevel.Anyone;

        public InvitationTarget InvitationTarget { get; } = InvitationTarget.None;

        public PhysicalAttribute PhysicalAttributeTarget { get; } = PhysicalAttribute.None;

        public LikeTarget LikeTarget { get; } = LikeTarget.None;

        public override string ToString()
        {
            return $"QuestionInfo({Id}, \"{Description}\")";
        }

        private static IEnumerable<QuestionInfo> LoadQuestions()
        {
            var serializer = new XmlSerializer(typeof(List<QuestionInfo>),
                new XmlRootAttribute($"{nameof(QuestionInfo)}s"));
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(typeof(QuestionInfo), "Resources.QuestionInfos.xml"))
            {
                if (stream != null)
                {
                    var result = serializer.Deserialize(stream) as List<QuestionInfo>;
                    Logger.LogFatal(result);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return new QuestionInfo[0];
        }

        public static QuestionInfo GetById(int questionId)
        {
            return _questionInfos.Value.TryGetValue(questionId, out var result) ? result : null;
        }
    }

    public class QuestionInfos : List<QuestionInfo> { }
}