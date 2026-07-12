using System;
using System.Collections.Generic;
using Civic.Simulation;
using NUnit.Framework;

namespace Civic.Tests.EditMode.UI
{
    public sealed class CivicLocalizationTests
    {
        [Test]
        public void Parser_AcceptsBomVersionEscapesAndConceptLinks()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource("test.yaml", "\uFEFFl_korean:\n concept_tax_rate:0 \"세율\"\n concept_tax_rate.desc:0 \"설명\\n둘째 줄\"\n effect.taxRateAdd:1 \"[concept_tax_rate] $VALUE|percent;sign=always;good=up$\""));

            var text = catalog.Resolve("effect.taxRateAdd", new Dictionary<string, object> { ["VALUE"] = 0.3d }, true);

            Assert.That(catalog.ConceptDescription("concept_tax_rate", false), Is.EqualTo("설명\n둘째 줄"));
            Assert.That(text, Does.Contain("<link=\"concept_tax_rate\">").And.Contain("+30%"));
        }

        [Test]
        public void Parser_RendersEscapedBracketsAsLiteralText()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource(
                "test.yaml",
                "l_korean:\n condition.status_met:0 \"<color=#62C982>\\[O\\]</color>\""));

            Assert.That(catalog.Resolve("condition.status_met", null, true), Is.EqualTo("<color=#62C982>[O]</color>"));
        }

        [Test]
        public void Parser_RejectsUnsupportedYamlSyntax()
        {
            Assert.Throws<CivicLocalizationException>(() => CivicLocalizationParser.Parse(
                new CivicLocalizationSource("bad.yaml", "l_korean:\n effect.bad: |\n  multiline")));
        }

        [Test]
        public void Formatter_UsesPercentSignsAndGoodDirection()
        {
            Assert.That(CivicLocalizationFormatter.Format(0.125d, "percent;sign=always;decimals=1;good=up", false), Is.EqualTo("+12.5%"));
            Assert.That(CivicLocalizationFormatter.Format(-0.2d, "percent;sign=always;good=down", true), Does.Contain("#62C982").And.Contain("-20%"));
        }

        [Test]
        public void Catalog_ReturnsRawKeyWhenEntryIsMissing()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource("test.yaml", "l_korean:\n known:0 \"값\""));
            Assert.That(catalog.Resolve("missing.effect"), Is.EqualTo("missing.effect"));
        }

        [Test]
        public void Validator_RejectsMissingEffectAndConceptDescription()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource("test.yaml", "l_korean:\n concept_value:0 \"값\"\n effect.present:0 \"[concept_value] $VALUE|number$\""));
            var exception = Assert.Throws<CivicLocalizationException>(() => CivicLocalizationValidator.Validate(catalog, new[] { "present", "missing" }));
            Assert.That(exception.Message, Does.Contain("effect.missing").And.Contain("concept_value.desc"));
        }

        [Test]
        public void Validator_RejectsUnclosedTextToken()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource(
                "bad-token.yaml",
                "l_korean:\n event.choice.no_effect:0 \"없음\"\n condition.status_met:0 \"[O]\"\n condition.status_unmet:0 \"[X]\"\n tooltip.continue:0 \"계속\"\n tooltip.depth_limit:0 \"제한\"\n effect.known:0 \"$TARGET 산출 $VALUE|number$\""));

            var exception = Assert.Throws<CivicLocalizationException>(() => CivicLocalizationValidator.Validate(catalog, new[] { "known" }));
            Assert.That(exception.Message, Does.Contain("unclosed localization token '$TARGET'"));
        }

        [Test]
        public void EffectPresenter_UsesRawEffectTypeWhenLocalizationKeyIsMissing()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource("test.yaml", "l_korean:\n effect.known:0 \"$TARGET$ $VALUE|number$\""));
            CivicLocalizationService.ResetForTests(catalog);
            try
            {
                Assert.That(CivicEffectText.Describe("unknownEffect", "target", 1d), Is.EqualTo("unknownEffect"));
            }
            finally
            {
                CivicLocalizationService.ResetForTests();
            }
        }

        [Test]
        public void EffectPresenter_ReplacesTargetToken()
        {
            var catalog = CivicLocalizationParser.Parse(new CivicLocalizationSource("test.yaml", "l_korean:\n effect.known:0 \"$TARGET$ $VALUE|number$\""));
            CivicLocalizationService.ResetForTests(catalog);
            try
            {
                var text = CivicEffectText.Describe("known", "*", 1d);
                Assert.That(text, Is.EqualTo("전체 1").And.Not.Contains("$TARGET"));
            }
            finally
            {
                CivicLocalizationService.ResetForTests();
            }
        }
    }
}
