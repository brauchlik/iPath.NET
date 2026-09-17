using Hl7.Fhir.Model;
using iPath.Application.Features.Questionnaires;
using static iPath.Test.xUnit2.Questionnaires.Fixtures;
using FhirCoding = Hl7.Fhir.Model.Coding;

namespace iPath.Test.xUnit2.Questionnaires;

/// <summary>
/// Answer extraction rules (SDC). Fixtures mirror the real Jundt forms: coded symptoms, an uncoded
/// quantity child of a coded parent, an item carrying both SNOMED and LOINC, a repeating choice and
/// an uncoded gate.
/// </summary>
public class QuestionnaireAnswerExtractorTests
{
    private readonly QuestionnaireAnswerExtractor _extractor = new();

    [Fact]
    public void Answered_true_produces_one_boolean_row_with_the_items_coding()
    {
        var q = QuestionnaireWith(Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));
        var r = new QuestionnaireResponse { Item = [Ans("sym.dysphagia", new FhirBoolean(true))] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        var row = rows[0];
        row.LinkId.Should().Be("sym.dysphagia");
        row.CodeSystem.Should().Be(QuestionnaireItemIndex.SnomedSystem);
        row.Code.Should().Be("40739000");
        row.CodeDisplay.Should().Be("Dysphagia");
        row.ValueType.Should().Be("boolean");
        row.Value.Should().Be("true");
    }

    [Fact]
    public void Answered_false_is_an_answer_and_not_a_missing_value()
    {
        var q = QuestionnaireWith(Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));
        var r = new QuestionnaireResponse { Item = [Ans("sym.dysphagia", new FhirBoolean(false))] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be("false");
    }

    [Fact]
    public void Unanswered_item_produces_no_row()
    {
        var q = QuestionnaireWith(Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")),
                                  Bool("sym.heartburn", "Heartburn", Snomed("16331000", "Heartburn")));
        var r = new QuestionnaireResponse { Item = [Ans("sym.dysphagia", new FhirBoolean(true))] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows.Should().NotContain(x => x.LinkId == "sym.heartburn");
    }

    [Fact]
    public void Quantity_child_of_a_coded_parent_inherits_the_parents_code()
    {
        var parent = Bool("sym.weightloss", "Weight loss", Snomed("89362005", "Weight loss"));
        parent.Item.Add(new Questionnaire.ItemComponent
        {
            LinkId = "sym.weightloss.kg",
            Text = "Amount lost (kg)",
            Type = Questionnaire.QuestionnaireItemType.Quantity,
            Extension = [new Extension(Fixtures.UnitOptionUrl, new FhirCoding { System = "http://unitsofmeasure.org", Code = "kg", Display = "kg" })]
        });
        var q = QuestionnaireWith(parent);

        var weightloss = Ans("sym.weightloss", new FhirBoolean(true));
        weightloss.Item.Add(Ans("sym.weightloss.kg", new Quantity { Value = 8m }));
        var r = new QuestionnaireResponse { Item = [weightloss] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(2);
        var kg = rows.Single(x => x.LinkId == "sym.weightloss.kg");
        kg.CodeSystem.Should().Be(QuestionnaireItemIndex.SnomedSystem);
        kg.Code.Should().Be("89362005.kg");
        kg.ValueType.Should().Be("quantity");
        kg.Value.Should().Be("8");
        kg.Unit.Should().Be("kg");
    }

    [Fact]
    public void The_same_concept_under_different_paths_resolves_to_the_same_key()
    {
        // digestive form: sym.pain.abdominal.location - hematology form: sym.other.pain.abdominal.location
        var digestiveParent = Bool("sym.pain.abdominal", "Abdominal pain", Snomed("21522001", "Abdominal pain"));
        digestiveParent.Item.Add(Choice("sym.pain.abdominal.location", "Location"));
        var digestiveQuestionnaire = QuestionnaireWith(digestiveParent);

        var hematologyParent = Bool("sym.other.pain.abdominal", "Abdominal pain", Snomed("21522001", "Abdominal pain"));
        hematologyParent.Item.Add(Choice("sym.other.pain.abdominal.location", "Location"));
        var hematologyQuestionnaire = QuestionnaireWith(hematologyParent);

        var fromDigestive = _extractor.Extract(
            new QuestionnaireResponse { Item = [Ans("sym.pain.abdominal.location", new FhirString("right"))] }, digestiveQuestionnaire);
        var fromHematology = _extractor.Extract(
            new QuestionnaireResponse { Item = [Ans("sym.other.pain.abdominal.location", new FhirString("left"))] }, hematologyQuestionnaire);

        fromDigestive.Should().HaveCount(1);
        fromHematology.Should().HaveCount(1);
        fromDigestive[0].Code.Should().Be("21522001.location");
        fromHematology[0].Code.Should().Be(fromDigestive[0].Code);
        fromHematology[0].CodeSystem.Should().Be(fromDigestive[0].CodeSystem);
    }

    [Fact]
    public void Prefers_snomed_and_keeps_the_other_codings()
    {
        var item = Bool("lab.thrombocytes", "Thrombocytes", Loinc("777-3", "Platelets"), Snomed("61928009", "Thrombocytes"));
        item.Type = Questionnaire.QuestionnaireItemType.Quantity;
        var q = QuestionnaireWith(item);
        var r = new QuestionnaireResponse { Item = [Ans("lab.thrombocytes", new Quantity { Value = 250m, Unit = "10*9/L" })] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows[0].CodeSystem.Should().Be(QuestionnaireItemIndex.SnomedSystem);
        rows[0].Code.Should().Be("61928009");
        rows[0].OtherCodings.Should().Contain("777-3");
    }

    [Fact]
    public void Repeating_answer_produces_one_row_per_value()
    {
        var q = QuestionnaireWith(new Questionnaire.ItemComponent
        {
            LinkId = "mat.cytology.types",
            Text = "Cytology material",
            Type = Questionnaire.QuestionnaireItemType.Choice,
            Repeats = true
        });
        var r = new QuestionnaireResponse
        {
            Item = [Ans("mat.cytology.types", new FhirString("Sputum"), new FhirString("Fine needle aspiration"))]
        };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(2);
        rows.Select(x => x.Value).Should().BeEquivalentTo("Sputum", "Fine needle aspiration");
    }

    [Fact]
    public void Uncoded_item_without_a_coded_ancestor_uses_the_dummy_system()
    {
        var q = QuestionnaireWith(Bool("sym.other.present", "Other symptoms present?"));
        var r = new QuestionnaireResponse { Item = [Ans("sym.other.present", new FhirBoolean(true))] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows[0].CodeSystem.Should().Be(QuestionnaireItemIndex.DummySystem);
        rows[0].Code.Should().Be("sym.other.present");
    }

    [Fact]
    public void Coded_answer_keeps_the_answers_own_code()
    {
        var q = QuestionnaireWith(new Questionnaire.ItemComponent
        {
            LinkId = "topo",
            Text = "Topography",
            Type = Questionnaire.QuestionnaireItemType.Choice,
            Code = [Snomed("39949000", "Topography")]
        });
        var r = new QuestionnaireResponse
        {
            Item = [Ans("topo", new FhirCoding { System = "http://terminology.hl7.org/CodeSystem/icd-o-3", Code = "C18.7", Display = "Sigmoid colon" })]
        };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows[0].ValueType.Should().Be("coding");
        rows[0].Value.Should().Be("http://terminology.hl7.org/CodeSystem/icd-o-3|C18.7");
        rows[0].ValueDisplay.Should().Be("Sigmoid colon");
    }

    [Fact]
    public void Empty_input_returns_nothing_and_does_not_throw()
    {
        _extractor.Extract(null, null).Should().BeEmpty();
        _extractor.Extract(new QuestionnaireResponse(), null).Should().BeEmpty();
    }

    [Fact]
    public void An_answer_without_a_definition_is_still_extracted_with_a_dummy_key()
    {
        var r = new QuestionnaireResponse { Item = [Ans("unknown.link", new FhirBoolean(true))] };

        var rows = _extractor.Extract(r, null);

        rows.Should().HaveCount(1);
        rows[0].CodeSystem.Should().Be(QuestionnaireItemIndex.DummySystem);
        rows[0].Code.Should().Be("unknown.link");
    }

    [Fact]
    public void Display_only_coding_keeps_the_display_as_the_value()
    {
        // LForms writes answer options as codings with a display and no system/code,
        // e.g. "Core biopsy" for mat.histology.types
        var q = QuestionnaireWith(Choice("mat.histology.types", "Histology material"));
        var r = new QuestionnaireResponse { Item = [Ans("mat.histology.types", new FhirCoding { Display = "Core biopsy" })] };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows[0].ValueType.Should().Be("coding");
        rows[0].Value.Should().Be("Core biopsy");
        rows[0].ValueDisplay.Should().Be("Core biopsy");
    }

    [Fact]
    public void Quantity_with_a_unit_keeps_value_and_unit_separate()
    {
        var q = QuestionnaireWith(new Questionnaire.ItemComponent
        {
            LinkId = "sym.pain.abdominal.duration",
            Text = "Duration",
            Type = Questionnaire.QuestionnaireItemType.Quantity,
            Code = [Snomed("21522001", "Abdominal pain")]
        });
        var r = new QuestionnaireResponse
        {
            Item = [Ans("sym.pain.abdominal.duration", new Quantity { Value = 30m, Unit = "days", Code = "d" })]
        };

        var rows = _extractor.Extract(r, q);

        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be("30");
        rows[0].Unit.Should().Be("days");
        rows[0].Code.Should().Be("21522001");
    }
}

public class QuestionnaireConformityCheckerTests
{
    private readonly QuestionnaireConformityChecker _checker = new();

    [Fact]
    public void Duplicate_linkIds_are_an_error()
    {
        var q = new Questionnaire
        {
            Item =
            [
                new Questionnaire.ItemComponent { LinkId = "sym.other", Text = "Other Symptoms", Type = Questionnaire.QuestionnaireItemType.Group },
                new Questionnaire.ItemComponent { LinkId = "sym.other", Text = "Other (please specify)", Type = Questionnaire.QuestionnaireItemType.String }
            ]
        };

        var findings = _checker.Check(q);

        findings.Should().Contain(f => f.Severity == "error" && f.LinkId == "sym.other");
    }

    [Fact]
    public void Uncoded_item_reports_the_generated_key_it_would_get()
    {
        var q = QuestionnaireWith(Bool("mat.cytology", "Cytology"));

        var findings = _checker.Check(q);

        findings.Should().Contain(f => f.LinkId == "mat.cytology" && f.Message.Contains("generated key"));
        findings.Should().NotContain(f => f.Severity == "error");
    }

    [Fact]
    public void Two_items_sharing_a_coding_are_reported()
    {
        var q = QuestionnaireWith(
            Bool("a", "A", Snomed("40739000", "Dysphagia")),
            Bool("b", "B", Snomed("40739000", "Dysphagia")));

        var findings = _checker.Check(q);

        findings.Should().Contain(f => f.LinkId == "b" && f.Message.Contains("40739000"));
    }

    [Fact]
    public void A_clean_questionnaire_reports_nothing()
    {
        var q = QuestionnaireWith(Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));

        _checker.Check(q).Should().BeEmpty();
    }
}

public class QuestionnaireSettingsTests
{
    [Fact]
    public void Extraction_is_on_when_the_flag_is_not_configured()
    {
        // questionnaires stored before the flag existed must keep extracting
        new QuestionnaireSettings().ShouldExtractAnswers().Should().BeTrue();
        new QuestionnaireSettings { ExtractAnswers = null }.ShouldExtractAnswers().Should().BeTrue();
        new QuestionnaireSettings { ExtractAnswers = true }.ShouldExtractAnswers().Should().BeTrue();
    }

    [Fact]
    public void Extraction_is_off_only_when_explicitly_switched_off()
    {
        new QuestionnaireSettings { ExtractAnswers = false }.ShouldExtractAnswers().Should().BeFalse();
    }
}

internal static class Fixtures
{
    public const string UnitOptionUrl = "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption";

    public static Questionnaire QuestionnaireWith(params Questionnaire.ItemComponent[] items)
        => new() { Item = [.. items] };

    public static Questionnaire.ItemComponent Bool(string linkId, string text, params FhirCoding[] codings) => new()
    {
        LinkId = linkId,
        Text = text,
        Type = Questionnaire.QuestionnaireItemType.Boolean,
        Code = [.. codings]
    };

    public static Questionnaire.ItemComponent Choice(string linkId, string text, params FhirCoding[] codings) => new()
    {
        LinkId = linkId,
        Text = text,
        Type = Questionnaire.QuestionnaireItemType.Choice,
        Code = [.. codings]
    };

    public static QuestionnaireResponse.ItemComponent Ans(string linkId, params DataType[] values)
    {
        var item = new QuestionnaireResponse.ItemComponent { LinkId = linkId };
        foreach (var value in values)
        {
            item.Answer.Add(new QuestionnaireResponse.AnswerComponent { Value = value });
        }
        return item;
    }

    public static FhirCoding Snomed(string code, string display)
        => new() { System = QuestionnaireItemIndex.SnomedSystem, Code = code, Display = display };

    public static FhirCoding Loinc(string code, string display)
        => new() { System = QuestionnaireItemIndex.LoincSystem, Code = code, Display = display };
}
