using System.Text;
using HandlebarsDotNet;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Common;

namespace PetSalon.Infrastructure.Pdf;

/// <summary>
/// 用 Handlebars.Net 把 contract.hbs 套上 ContractRenderData 並轉成 HTML。
/// 模擬 pet_salon 後端 pdf-generator/index.js 的 buildViewModel 行為。
/// </summary>
public sealed class ContractTemplateRenderer
{
    private readonly Lazy<HandlebarsTemplate<object, object>> _template;
    private readonly Lazy<string> _fontDataUri;

    public ContractTemplateRenderer(string templatePath, string? fontPath = null)
    {
        _template = new Lazy<HandlebarsTemplate<object, object>>(() => LoadTemplate(templatePath));
        _fontDataUri = new Lazy<string>(() => LoadFontDataUri(fontPath));
    }

    private static HandlebarsTemplate<object, object> LoadTemplate(string templatePath)
    {
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("找不到契約模板", templatePath);
        var src = File.ReadAllText(templatePath, Encoding.UTF8);
        var hb = Handlebars.Create(new HandlebarsConfiguration
        {
            // 預設 encoder 會把中文字逃成 &#nnnn; 數字實體 → 跟 JS 版 Handlebars 不一致。
            // 我們的資料來源全是內部建模，不需 HTML escape。
            TextEncoder = null,
        });
        RegisterHelpers(hb);
        return hb.Compile(src);
    }

    private static void RegisterHelpers(IHandlebars hb)
    {
        // {{#ifEq a b}}...{{/ifEq}} — 等值
        hb.RegisterHelper("ifEq", (writer, options, context, args) =>
        {
            if (args.Length < 2) return;
            var a = args[0]?.ToString() ?? string.Empty;
            var b = args[1]?.ToString() ?? string.Empty;
            // Handle bool 比較
            if (args[0] is bool ba && bool.TryParse(b, out var bb))
            {
                if (ba == bb) options.Template(writer, context); else options.Inverse(writer, context);
                return;
            }
            if (string.Equals(a, b, StringComparison.Ordinal))
                options.Template(writer, context);
            else
                options.Inverse(writer, context);
        });

        // {{#includes arr value}}...{{/includes}} — 陣列包含
        hb.RegisterHelper("includes", (writer, options, context, args) =>
        {
            if (args.Length < 2) return;
            var arr = args[0];
            var target = args[1]?.ToString() ?? string.Empty;
            var ok = false;
            if (arr is System.Collections.IEnumerable en && arr is not string)
            {
                foreach (var x in en)
                {
                    if (string.Equals(x?.ToString(), target, StringComparison.Ordinal)) { ok = true; break; }
                }
            }
            if (ok) options.Template(writer, context); else options.Inverse(writer, context);
        });
    }

    private static string LoadFontDataUri(string? fontPath)
    {
        if (string.IsNullOrEmpty(fontPath) || !File.Exists(fontPath)) return string.Empty;
        var bytes = File.ReadAllBytes(fontPath);
        return $"data:font/ttf;base64,{Convert.ToBase64String(bytes)}";
    }

    public string Render(ContractRenderData data)
    {
        var view = BuildViewModel(data);
        return _template.Value(view);
    }

    private object BuildViewModel(ContractRenderData data)
    {
        var rocStr = RocDate.ToRocString(data.GroomingRecord.ServiceDate);
        var (rocY, rocM, rocD) = ParseRocDate(rocStr);

        var ownerSig = (data.OwnerSignaturePng is { Length: > 0 })
            ? $"data:image/png;base64,{Convert.ToBase64String(data.OwnerSignaturePng)}"
            : string.Empty;

        var hospitalLine = string.IsNullOrWhiteSpace(data.HospitalName) || data.HospitalName == "安欣動物醫院"
            ? string.Empty
            : $"{data.HospitalName} / {data.HospitalPhone} / {data.HospitalAddress}";

        // 用 Dictionary<string, object?> 確保 Handlebars.Net 能正確 dot-path 解析
        var owner = new Dictionary<string, object?>
        {
            ["name"] = data.Owner.Name,
            ["nationalId"] = data.Owner.NationalId,
            ["phone"] = data.Owner.Phone,
            ["address"] = data.Owner.Address,
            ["emergencyContactName"] = data.Owner.EmergencyContactName,
            ["emergencyContactPhone"] = data.Owner.EmergencyContactPhone,
            ["emergencyContactRelationship"] = data.Owner.EmergencyContactRelationship,
            ["preferredAnimalHospital"] = data.HospitalName,
            ["preferredAnimalHospitalPhone"] = data.HospitalPhone,
            ["preferredAnimalHospitalAddress"] = data.HospitalAddress,
            ["isStoredValueCustomer"] = data.Owner.IsStoredValueCustomer,
            ["storedValueBalance"] = data.Owner.StoredValueBalance,
        };

        var pet = new Dictionary<string, object?>
        {
            ["name"] = data.Pet.Name,
            ["species"] = data.Pet.Species,
            ["breed"] = data.Pet.Breed,
            ["gender"] = data.Pet.Gender,
            ["age"] = data.Pet.Age,
            ["isNeutered"] = data.Pet.IsNeutered,
            ["chipNumber"] = data.Pet.ChipNumber ?? string.Empty,
            ["unregisteredIdMethod"] = data.Pet.UnregisteredIdMethod ?? string.Empty,
            ["personality"] = data.GroomingRecord.Personality.Count > 0
                ? (object)data.GroomingRecord.Personality
                : data.Pet.Personality,
            ["medicalHistory"] = data.GroomingRecord.MedicalHistory.Count > 0
                ? (object)data.GroomingRecord.MedicalHistory
                : data.Pet.MedicalHistory,
        };

        // --- 病史「以上皆無」判定 ---
        // 條件：MedicalHistory 包含 "以上皆無" 或 MedicalHistory 為空陣列
        var effectiveMedicalHistory = data.GroomingRecord.MedicalHistory.Count > 0
            ? data.GroomingRecord.MedicalHistory
            : data.Pet.MedicalHistory;
        var medicalHistoryNone =
            effectiveMedicalHistory.Count == 0 ||
            effectiveMedicalHistory.Contains("以上皆無");

        // --- 身體狀態檢視（以 GroomingRecord.PhysicalExamination 為準）---
        var exam = data.GroomingRecord.PhysicalExamination;

        // fur 逗號分隔 → 拆成陣列，e.g. "異常,打結" → ["異常","打結"]
        // 若只是 "正常" 則 furValues = ["正常"]
        var furValues = (exam.Fur ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Note 顯示條件：欄位包含「異常」且對應 Note 非空
        bool EyesHasAbnormal   = (exam.Eyes   ?? string.Empty).Contains("異常");
        bool EarsHasAbnormal   = (exam.Ears   ?? string.Empty).Contains("異常");
        bool TeethHasAbnormal  = (exam.Teeth  ?? string.Empty).Contains("異常");
        bool LimbsHasAbnormal  = (exam.Limbs  ?? string.Empty).Contains("異常");
        bool SkinHasAbnormal   = (exam.Skin   ?? string.Empty).Contains("異常");
        bool FurHasAbnormal    = furValues.Contains("異常");

        var physicalExamination = new Dictionary<string, object?>
        {
            ["eyes"]              = exam.Eyes,
            ["ears"]              = exam.Ears,
            ["teeth"]             = exam.Teeth,
            ["limbs"]             = exam.Limbs,
            ["skin"]              = exam.Skin,
            ["fur"]               = exam.Fur,
            ["furValues"]         = furValues,
            // Note 欄位
            ["eyesNote"]          = exam.EyesNote,
            ["earsNote"]          = exam.EarsNote,
            ["teethNote"]         = exam.TeethNote,
            ["limbsNote"]         = exam.LimbsNote,
            ["skinNote"]          = exam.SkinNote,
            ["furNote"]           = exam.FurNote,
            // Note 可見 flag（異常且 Note 非空）
            ["eyesNoteVisible"]   = EyesHasAbnormal  && !string.IsNullOrWhiteSpace(exam.EyesNote),
            ["earsNoteVisible"]   = EarsHasAbnormal  && !string.IsNullOrWhiteSpace(exam.EarsNote),
            ["teethNoteVisible"]  = TeethHasAbnormal && !string.IsNullOrWhiteSpace(exam.TeethNote),
            ["limbsNoteVisible"]  = LimbsHasAbnormal && !string.IsNullOrWhiteSpace(exam.LimbsNote),
            ["skinNoteVisible"]   = SkinHasAbnormal  && !string.IsNullOrWhiteSpace(exam.SkinNote),
            // Q8：FurNote 只在 Fur 包含「異常」時輸出
            ["furNoteVisible"]    = FurHasAbnormal   && !string.IsNullOrWhiteSpace(exam.FurNote),
        };

        var record = new Dictionary<string, object?>
        {
            ["services"] = data.GroomingRecord.Services.Select(s => new Dictionary<string, object?>
            {
                ["item"] = s.Item,
                ["price"] = s.Price,
            }).ToArray(),
            ["totalCost"] = data.GroomingRecord.TotalCost,
            ["storedValueDeduction"] = data.GroomingRecord.StoredValueDeduction,
            ["cashPayment"] = data.GroomingRecord.CashPayment,
            ["serviceDate"] = data.GroomingRecord.ServiceDate.ToString("yyyy-MM-dd"),
            ["serviceTime"] = data.GroomingRecord.ServiceTime.ToString("HH:mm"),
            ["ownerNotes"] = data.GroomingRecord.OwnerNotes,
            ["shopNotes"] = data.GroomingRecord.ShopNotes,
            ["otherNotes"] = data.GroomingRecord.OtherNotes,
            ["ownerSignatureDataUrl"] = ownerSig,
        };
        var hospital = new Dictionary<string, object?>
        {
            ["name"] = data.HospitalName,
            ["phone"] = data.HospitalPhone,
            ["address"] = data.HospitalAddress,
        };

        return new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["pet"] = pet,
            ["record"] = record,
            ["hospital"] = hospital,
            ["physicalExamination"] = physicalExamination,
            ["medicalHistoryNone"] = medicalHistoryNone,
            ["rocDate"] = rocStr,
            ["rocYear"] = rocY,
            ["rocMonth"] = rocM,
            ["rocDay"] = rocD,
            ["has洗澡"] = data.GroomingRecord.Services.Any(s => s.Item == "洗澡"),
            ["has美容"] = data.GroomingRecord.Services.Any(s => s.Item == "美容"),
            ["has其他"] = data.GroomingRecord.Services.Any(s => s.Item == "其他"),
            ["price洗澡"] = PriceOf(data, "洗澡"),
            ["price美容"] = PriceOf(data, "美容"),
            ["price其他"] = PriceOf(data, "其他"),
            ["ownerPreferredHospitalLine"] = hospitalLine,
            ["hospitalTarget"] = string.IsNullOrEmpty(hospitalLine) ? "店家" : "飼主",
            ["storedValueDisplay"] = data.Owner.IsStoredValueCustomer
                ? data.Owner.StoredValueBalance.ToString("0")
                : string.Empty,
            ["fontDataUri"] = _fontDataUri.Value,
        };
    }

    private static string PriceOf(ContractRenderData data, string item)
    {
        var found = data.GroomingRecord.Services.FirstOrDefault(s => s.Item == item);
        return found is null ? string.Empty : found.Price.ToString("0");
    }

    private static (string year, string month, string day) ParseRocDate(string rocStr)
    {
        // "民國 113 年 5 月 16 日"
        var m = System.Text.RegularExpressions.Regex.Match(rocStr, @"(\d{1,3})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})\s*日");
        if (!m.Success) return ("", "", "");
        return (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
    }
}
