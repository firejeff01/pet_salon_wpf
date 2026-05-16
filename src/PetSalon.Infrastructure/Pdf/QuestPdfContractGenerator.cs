using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;
using PetSalon.Core.Constants;
using PetSalon.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PetSalon.Infrastructure.Pdf;

public sealed class QuestPdfContractGenerator : IPdfGenerator
{
    private const string FontFamily = "Microsoft JhengHei";

    static QuestPdfContractGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<ContractGenerateOutput> GenerateContractAsync(
        ContractRenderData data,
        string outputDir,
        int nextVersion,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var fileName = BuildFileName(data, nextVersion);
        var absolutePath = Path.GetFullPath(Path.Combine(outputDir, fileName));

        Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(18, Unit.Millimetre);
            p.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(10).LineHeight(1.45f));

            p.Header().AlignCenter().Column(col =>
            {
                col.Item().Text("貳寶寵物美容工坊")
                    .FontSize(15).SemiBold().FontColor("#C83F6D");
                col.Item().Text("犬貓美容定型化契約書")
                    .FontSize(13).SemiBold();
            });

            p.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(8);

                col.Item().Element(e => Section(e, "一、飼主資料"));
                col.Item().Element(e => Pairs(e, new[]
                {
                    ("姓名", data.Owner.Name),
                    ("身分證字號", data.Owner.NationalId),
                    ("聯絡電話", data.Owner.Phone),
                    ("通訊地址", data.Owner.Address),
                    ("緊急聯絡人", data.Owner.EmergencyContactName),
                    ("緊急聯絡電話", data.Owner.EmergencyContactPhone),
                    ("與飼主關係", data.Owner.EmergencyContactRelationship),
                }));

                col.Item().Element(e => Section(e, "二、寵物資料"));
                col.Item().Element(e => Pairs(e, new[]
                {
                    ("名稱", data.Pet.Name),
                    ("物種", data.Pet.Species),
                    ("品種", data.Pet.Breed),
                    ("性別", data.Pet.Gender),
                    ("年齡", data.Pet.Age),
                    ("是否結紮", data.Pet.IsNeutered ? "是" : "否"),
                    ("晶片號碼", data.Pet.ChipNumber ?? string.Empty),
                    ("無晶片識別方式", data.Pet.UnregisteredIdMethod ?? string.Empty),
                    ("個性", string.Join("、", data.Pet.Personality)),
                }));

                col.Item().Element(e => Section(e, "三、身體檢查"));
                col.Item().Element(e => Pairs(e, new[]
                {
                    ("眼睛", data.GroomingRecord.PhysicalExamination.Eyes),
                    ("耳朵", data.GroomingRecord.PhysicalExamination.Ears),
                    ("牙齒", data.GroomingRecord.PhysicalExamination.Teeth),
                    ("四肢", data.GroomingRecord.PhysicalExamination.Limbs),
                    ("皮膚", data.GroomingRecord.PhysicalExamination.Skin),
                    ("皮毛", data.GroomingRecord.PhysicalExamination.Fur),
                }));

                col.Item().Element(e => Section(e, "四、病史"));
                col.Item().Text(text =>
                {
                    var entries = data.GroomingRecord.MedicalHistory ?? new();
                    if (entries.Count == 0) text.Span("無");
                    else text.Span(string.Join("、", entries));
                    if (!string.IsNullOrWhiteSpace(data.GroomingRecord.MedicalHistoryOther))
                    {
                        text.Span("　其他：" + data.GroomingRecord.MedicalHistoryOther);
                    }
                });

                col.Item().Element(e => Section(e, "五、本次美容服務"));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                    });
                    t.Header(h =>
                    {
                        h.Cell().Element(HeadCell).Text("項目").SemiBold();
                        h.Cell().Element(HeadCell).Text("金額（NTD）").SemiBold();
                    });
                    foreach (var s in data.GroomingRecord.Services)
                    {
                        t.Cell().Element(BodyCell).Text(s.Item);
                        t.Cell().Element(BodyCell).Text($"{s.Price:N0}");
                    }
                    t.Cell().Element(BodyCell).Text("合計").SemiBold();
                    t.Cell().Element(BodyCell).Text($"{data.GroomingRecord.TotalCost:N0}").SemiBold();

                    if (data.GroomingRecord.StoredValueDeduction > 0)
                    {
                        t.Cell().Element(BodyCell).Text("儲值扣抵");
                        t.Cell().Element(BodyCell).Text($"-{data.GroomingRecord.StoredValueDeduction:N0}");
                        t.Cell().Element(BodyCell).Text("現金應付").SemiBold();
                        t.Cell().Element(BodyCell).Text($"{data.GroomingRecord.CashPayment:N0}").SemiBold();
                    }
                });

                col.Item().Element(e => Section(e, "六、指定動物醫院"));
                col.Item().Element(e => Pairs(e, new[]
                {
                    ("名稱", data.HospitalName),
                    ("電話", data.HospitalPhone),
                    ("地址", data.HospitalAddress),
                }));

                if (!string.IsNullOrWhiteSpace(data.GroomingRecord.OwnerNotes) ||
                    !string.IsNullOrWhiteSpace(data.GroomingRecord.ShopNotes) ||
                    !string.IsNullOrWhiteSpace(data.GroomingRecord.OtherNotes))
                {
                    col.Item().Element(e => Section(e, "七、備註"));
                    col.Item().Element(e => Pairs(e, new[]
                    {
                        ("飼主備註", data.GroomingRecord.OwnerNotes),
                        ("店家備註", data.GroomingRecord.ShopNotes),
                        ("其他備註", data.GroomingRecord.OtherNotes),
                    }));
                }

                col.Item().Element(e => Section(e, "八、雙方簽署"));
                col.Item().Row(r =>
                {
                    r.RelativeItem().Border(1).BorderColor("#F0B9C8").Padding(8).Column(cb =>
                    {
                        cb.Item().Text($"甲方（飼主）：{data.Owner.Name}").SemiBold();
                        cb.Item().Height(60).AlignCenter().AlignMiddle().Element(e =>
                        {
                            if (data.OwnerSignaturePng is { Length: > 0 }) e.Image(data.OwnerSignaturePng).FitArea();
                            else e.Text("(未簽名)").Italic().FontColor("#A08E7E");
                        });
                    });
                    r.ConstantItem(12);
                    r.RelativeItem().Border(1).BorderColor("#F0B9C8").Padding(8).Column(cb =>
                    {
                        cb.Item().Text("乙方（貳寶寵物美容工坊）").SemiBold();
                        cb.Item().Height(60);
                    });
                });

                col.Item().PaddingTop(10).Text(RocDate.ToRocString(data.GroomingRecord.ServiceDate)).SemiBold();
            });

            p.Footer().AlignRight().Text(t =>
            {
                t.Span($"v{nextVersion}　").FontSize(9).FontColor("#A08E7E");
                t.CurrentPageNumber().FontSize(9);
                t.Span(" / ").FontSize(9);
                t.TotalPages().FontSize(9);
            });
        })).GeneratePdf(absolutePath);

        return Task.FromResult(new ContractGenerateOutput(absolutePath, nextVersion));
    }

    private static string BuildFileName(ContractRenderData data, int version)
    {
        var date = data.GroomingRecord.ServiceDate.ToString("yyyyMMdd");
        var time = data.GroomingRecord.ServiceTime.ToString("HHmm");
        var pet = FileNameSanitizer.Sanitize(data.Pet.Name);
        var owner = FileNameSanitizer.Sanitize(data.Owner.Name);
        return $"{date}_{time}_{pet}_{owner}_契約_v{version}.pdf";
    }

    private static void Section(IContainer container, string title)
    {
        container.BorderBottom(1).BorderColor("#F0B9C8").PaddingBottom(2)
            .Text(title).FontSize(12).SemiBold().FontColor("#C83F6D");
    }

    private static void Pairs(IContainer container, IEnumerable<(string Label, string Value)> rows)
    {
        var list = rows.ToList();
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(5);
            });
            foreach (var (label, value) in list)
            {
                t.Cell().Element(HeadCell).Text(label).SemiBold();
                t.Cell().Element(BodyCell).Text(string.IsNullOrEmpty(value) ? "—" : value);
            }
        });
    }

    private static IContainer HeadCell(IContainer c) =>
        c.Border(0.5f).BorderColor("#F0B9C8").Background("#FFF5F8").Padding(5);

    private static IContainer BodyCell(IContainer c) =>
        c.Border(0.5f).BorderColor("#F0B9C8").Padding(5);
}
