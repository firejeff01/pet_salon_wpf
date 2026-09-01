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

    private static ContractRenderData Data(byte[]? groomerSignature, byte[]? managerSignature)
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
            groomerSignature,
            managerSignature);
    }

    [Fact]
    public void Render_puts_groomer_signature_in_groomer_slot_and_manager_signature_in_party_b_slot()
    {
        byte[] groomer = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        byte[] manager = [0x89, 0x50, 0x4E, 0x47, 4, 5, 6];

        var html = Renderer().Render(Data(groomer, manager));
        var groomerUrl = $"data:image/png;base64,{Convert.ToBase64String(groomer)}";
        var managerUrl = $"data:image/png;base64,{Convert.ToBase64String(manager)}";

        html.Split(groomerUrl).Should().HaveCount(2, "美容人員簽名應只出現一次");
        html.Split(managerUrl).Should().HaveCount(2, "負責人簽名應只出現一次");
        html.Should().Contain($"美容人員簽名：<span class=\"blank wide\"><img class=\"signature-img\" src=\"{groomerUrl}\"");
        html.Should().Contain($"乙方簽章：<span class=\"blank wide\"><img class=\"signature-img\" src=\"{managerUrl}\"");
        html.Should().Contain("飼主簽名：<span class=\"blank wide\"></span>");
        html.Should().Contain("甲方簽章：<span class=\"blank wide\"></span>");
    }

    [Fact]
    public void Render_leaves_party_b_blank_when_only_groomer_signature_is_selected()
    {
        byte[] groomer = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        var html = Renderer().Render(Data(groomer, null));

        html.Should().Contain("美容人員簽名：<span class=\"blank wide\"><img class=\"signature-img\"");
        html.Should().Contain("乙方簽章：<span class=\"blank wide\"></span>");
    }

    [Fact]
    public void Render_leaves_groomer_slot_blank_when_only_manager_signature_is_selected()
    {
        byte[] manager = [0x89, 0x50, 0x4E, 0x47, 4, 5, 6];

        var html = Renderer().Render(Data(null, manager));

        html.Should().Contain("美容人員簽名：<span class=\"blank wide\"></span>");
        html.Should().Contain("乙方簽章：<span class=\"blank wide\"><img class=\"signature-img\"");
    }

    [Fact]
    public void Render_leaves_all_signature_slots_blank_when_shop_signatures_are_missing()
    {
        var html = Renderer().Render(Data(null, null));

        html.Should().NotContain("class=\"signature-img\"");
        html.Should().Contain("美容人員簽名：<span class=\"blank wide\"></span>");
        html.Should().Contain("乙方簽章：<span class=\"blank wide\"></span>");
    }
}
