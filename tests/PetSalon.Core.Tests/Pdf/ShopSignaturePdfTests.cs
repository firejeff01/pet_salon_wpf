using FluentAssertions;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Entities;
using PetSalon.Infrastructure.Pdf;
using Xunit;

namespace PetSalon.Core.Tests.Pdf;

public sealed class ShopSignaturePdfTests
{
    private static ContractTemplateRenderer Renderer()
    {
        var output = Path.GetDirectoryName(typeof(PuppeteerSharpContractGenerator).Assembly.Location)!;
        return new ContractTemplateRenderer(
            Path.Combine(output, "Pdf", "Templates", "contract.hbs"),
            Path.Combine(output, "Pdf", "Assets", "kaiu.ttf"));
    }

    private static ContractRenderData Data(byte[]? shopSignature)
    {
        var owner = new Owner { OwnerId = "owner_1", Name = "王小明" };
        var pet = new Pet { PetId = "pet_1", OwnerId = owner.OwnerId, Name = "毛毛", Species = "犬", Gender = "公" };
        var appointment = new Appointment
        {
            AppointmentId = "appt_1",
            OwnerId = owner.OwnerId,
            PetId = pet.PetId,
            Date = new DateOnly(2026, 8, 18),
            Time = new TimeOnly(10, 0),
        };
        var record = new GroomingRecord
        {
            GroomingRecordId = "record_1",
            AppointmentId = appointment.AppointmentId,
            ServiceDate = appointment.Date,
            ServiceTime = appointment.Time,
        };
        return new ContractRenderData(
            owner,
            pet,
            appointment,
            record,
            null,
            "安欣動物醫院",
            "03-3367775",
            "桃園市桃園區中福街60號",
            shopSignature);
    }

    [Fact]
    public void Render_embeds_shop_signature_only_in_groomer_and_party_b_slots()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        var html = Renderer().Render(Data(png));
        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(png)}";

        html.Split(dataUrl).Should().HaveCount(3, "同一簽名應只出現兩次");
        html.Should().Contain("美容人員簽名：<span class=\"blank wide\"><img class=\"signature-img\"");
        html.Should().Contain("乙方簽章：<span class=\"blank wide\"><img class=\"signature-img\"");
        html.Should().Contain("飼主簽名：<span class=\"blank wide\"></span>");
        html.Should().Contain("甲方簽章：<span class=\"blank wide\"></span>");
    }

    [Fact]
    public void Render_leaves_all_signature_slots_blank_when_shop_signature_is_missing()
    {
        var html = Renderer().Render(Data(null));

        html.Should().NotContain("class=\"signature-img\"");
        html.Should().Contain("美容人員簽名：<span class=\"blank wide\"></span>");
        html.Should().Contain("乙方簽章：<span class=\"blank wide\"></span>");
    }
}
