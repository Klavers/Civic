using System.Collections.Generic;

namespace Civic.Features
{
    public static class CivicFeatureRegistry
    {
        public const string Prestige = "meta.prestige";
        public const string Achievements = "progress.achievements";
        public const string StartCivilizations = "identity.startCivilizations";
        public const string NationFormation = "identity.nationFormation";
        public const string Politics = "governance.politics";
        public const string Events = "narrative.events";
        public const string Wonders = "projects.wonders";
        public const string GreatPeople = "people.greatPeople";

        private static readonly CivicFeatureDefinition[] FeatureDefinitions =
        {
            new CivicFeatureDefinition(Prestige, "환생과 문명 유산", "런 종료 점수, 환생 포인트와 영구 유산의 기반입니다."),
            new CivicFeatureDefinition(Achievements, "도전과제", "일반·특수 도전과제의 조건과 보상을 추적합니다."),
            new CivicFeatureDefinition(StartCivilizations, "시작 문명", "새 런에서 서로 다른 특성을 가진 시작 문명을 선택합니다."),
            new CivicFeatureDefinition(NationFormation, "문명·국가 설립", "조건을 만족해 새로운 문명이나 국가를 수립합니다."),
            new CivicFeatureDefinition(Politics, "정치·사회 체계", "정부, 선거, 복지와 교육 제도를 변경합니다."),
            new CivicFeatureDefinition(Events, "조건부 이벤트", "게임 상태와 확률에 따른 사건과 선택지를 추가합니다."),
            new CivicFeatureDefinition(Wonders, "불가사의", "대규모 자원을 투입하는 장기 건설 프로젝트를 추가합니다."),
            new CivicFeatureDefinition(GreatPeople, "위인·인물", "인물의 생성, 배치, 활동과 소멸 생명주기를 추가합니다.")
        };

        private static readonly CivicFeatureIntegrationDefinition[] IntegrationDefinitions =
        {
            Integration("integration.achievementPrestige", Prestige, Achievements),
            Integration("integration.prestigeCivilizations", Prestige, StartCivilizations),
            Integration("integration.civilizationNations", StartCivilizations, NationFormation),
            Integration("integration.nationPolitics", NationFormation, Politics),
            Integration("integration.reformEvents", Politics, Events),
            Integration("integration.wonderEvents", Wonders, Events),
            Integration("integration.peopleEvents", GreatPeople, Events),
            Integration("integration.wonderPeople", Wonders, GreatPeople),
            Integration(
                "integration.fullLegacyCampaign",
                Prestige,
                Achievements,
                StartCivilizations,
                NationFormation,
                Politics,
                Events,
                Wonders,
                GreatPeople)
        };

        public static IReadOnlyList<CivicFeatureDefinition> Features => FeatureDefinitions;
        public static IReadOnlyList<CivicFeatureIntegrationDefinition> Integrations => IntegrationDefinitions;

        private static CivicFeatureIntegrationDefinition Integration(string id, params string[] requiresAll)
        {
            return new CivicFeatureIntegrationDefinition(id, requiresAll);
        }
    }
}
