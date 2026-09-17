using Hl7.Fhir.Model;
using iPath.Application.Features.Questionnaires;
using iPath.Application.Features.Questionnaires.Queries;
using static iPath.Test.xUnit2.Questionnaires.Fixtures;

namespace iPath.Test.xUnit2.Questionnaires;

public class AnswerCatalogBuilderTests
{
    private static CatalogFormInput Form(string id, params Questionnaire.ItemComponent[] items)
        => new(id, id, 1, QuestionnaireWith(items));

    [Fact]
    public void Concept_keys_match_the_keys_the_extractor_writes()
    {
        var form = Form("digestive", Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));

        var concept = AnswerCatalogBuilder.BuildConcepts([form]).Single();
        var row = new QuestionnaireAnswerExtractor()
            .Extract(new QuestionnaireResponse { Item = [Ans("sym.dysphagia", new FhirBoolean(true))] }, form.Definition)
            .Single();

        concept.Key.Should().Be(AnswerConcept.KeyFor(row.CodeSystem, row.Code));
        concept.Display.Should().Be("Dysphagia");
        concept.HasCode.Should().BeTrue();
    }

    [Fact]
    public void A_quantity_child_is_keyed_from_its_coded_parent()
    {
        var parent = Bool("sym.weightloss", "Weight loss", Snomed("89362005", "Weight loss"));
        parent.Item.Add(new Questionnaire.ItemComponent
        {
            LinkId = "sym.weightloss.kg",
            Text = "Amount lost (kg)",
            Type = Questionnaire.QuestionnaireItemType.Quantity
        });

        var concept = AnswerCatalogBuilder.BuildConcepts([Form("digestive", parent)])
            .Single(c => c.LinkIds.Contains("sym.weightloss.kg"));

        concept.Code.Should().Be("89362005.kg");
        concept.Key.Should().Be(AnswerConcept.KeyFor(QuestionnaireItemIndex.SnomedSystem, "89362005.kg"));
        concept.ValueType.Should().Be("quantity");
    }

    [Fact]
    public void The_same_concept_in_two_forms_becomes_one_entry()
    {
        var digestive = Form("digestive", Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));
        var hematology = Form("hematology", Bool("sym.other.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));

        var concepts = AnswerCatalogBuilder.BuildConcepts([digestive, hematology]);

        concepts.Should().HaveCount(1);
        concepts[0].LinkIds.Should().BeEquivalentTo("sym.dysphagia", "sym.other.dysphagia");
        concepts[0].Forms.Should().BeEquivalentTo("digestive", "hematology");
    }

    [Fact]
    public void Uncoded_items_are_marked_and_ordered_after_the_coded_ones()
    {
        var form = Form("digestive",
            Bool("zz.other.present", "Other present?"),
            Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));

        var concepts = AnswerCatalogBuilder.BuildConcepts([form]);

        concepts.Should().HaveCount(2);
        concepts[0].Code.Should().Be("40739000");
        concepts[1].HasCode.Should().BeFalse();
        concepts[1].Key.Should().Be(AnswerConcept.KeyFor(QuestionnaireItemIndex.DummySystem, "zz.other.present"));
        concepts[1].Display.Should().Be("zz.other.present");
    }

    [Fact]
    public void Value_type_repeats_and_options_are_reported()
    {
        var item = new Questionnaire.ItemComponent
        {
            LinkId = "mat.cytology.types",
            Text = "Cytology material",
            Type = Questionnaire.QuestionnaireItemType.Choice,
            Repeats = true,
            Code = [Snomed("88480006", "Cytology material")],
            AnswerOption =
            [
                new Questionnaire.AnswerOptionComponent { Value = new FhirString("Sputum") },
                new Questionnaire.AnswerOptionComponent { Value = new FhirString("Pap smear") }
            ]
        };

        var concept = AnswerCatalogBuilder.BuildConcepts([Form("material", item)]).Single();

        concept.ValueType.Should().Be("choice");
        concept.Repeats.Should().BeTrue();
        concept.Options.Should().BeEquivalentTo("Sputum", "Pap smear");
    }

    [Fact]
    public void Answer_counts_are_attached_by_key()
    {
        var form = Form("digestive", Bool("sym.dysphagia", "Dysphagia", Snomed("40739000", "Dysphagia")));
        var key = AnswerConcept.KeyFor(QuestionnaireItemIndex.SnomedSystem, "40739000");

        var concept = AnswerCatalogBuilder.BuildConcepts([form],
            new Dictionary<string, CatalogAnswerCounts> { [key] = new(3, 4) }).Single();

        concept.AnsweredCases.Should().Be(3);
        concept.AnsweredRows.Should().Be(4);
    }
}
