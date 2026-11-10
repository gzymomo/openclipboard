using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OpenClipboard.App.Services;

public sealed class LocalizationService
{
    private static readonly (string Code, string Name)[] SupportedLanguages =
    [
        ("zh-CN", "简体中文"),
        ("en-US", "English"),
        ("de-DE", "Deutsch"),
        ("fr-FR", "Français"),
        ("ja-JP", "日本語"),
        ("ko-KR", "한국어"),
        ("ru-RU", "Русский"),
        ("es-ES", "Español"),
        ("pt-PT", "Português"),
        ("th-TH", "ไทย"),
        ("vi-VN", "Tiếng Việt")
    ];

    private readonly IReadOnlyDictionary<string, string> _english;
    private IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<LanguageOption> Languages { get; } =
        SupportedLanguages.Select(language => new LanguageOption(language.Code, language.Name)).ToArray();

    public string CurrentLanguage { get; private set; } = "en-US";

    public LocalizationService(string language)
    {
        _english = LoadDictionary("en-US");
        SetLanguage(language);
    }

    public void SetLanguage(string language)
    {
        var supportedLanguage = SupportedLanguages.FirstOrDefault(
            item => string.Equals(item.Code, language, StringComparison.OrdinalIgnoreCase));
        CurrentLanguage = supportedLanguage.Code ?? "en-US";
        _current = LoadDictionary(CurrentLanguage);
        var culture = CultureInfo.GetCultureInfo(CurrentLanguage);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public string Get(string key)
    {
        if (_current.TryGetValue(key, out var value) || _english.TryGetValue(key, out value))
        {
            return value;
        }

        if (CurrentLanguage == "zh-CN" && ChineseText.TryGetValue(key, out value))
        {
            return value;
        }

        if (LanguageFallbacks.TryGetValue(CurrentLanguage, out var languageText)
            && languageText.TryGetValue(key, out value))
        {
            return value;
        }

        return EnglishText.TryGetValue(key, out value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> EnglishText = new Dictionary<string, string>
    {
        ["CrossPlatform_Settings"] = "Settings",
        ["CrossPlatform_OpenApp"] = "Open OpenClipboard",
        ["CrossPlatform_Save"] = "Save",
        ["CrossPlatform_Disabled"] = "Disabled",
        ["CrossPlatform_Hotkey_Help"] = "Press a key combination to assign it. Press Escape in Quick Copy to disable it.",
        ["CrossPlatform_Hotkey_Unsupported"] = "Use a letter, number, or function key.",
        ["CrossPlatform_Hotkey_Duplicate"] = "The two shortcuts must be different.",
        ["CrossPlatform_SelectHistoryMode"] = "Select a history retention period.",
        ["CrossPlatform_Save_Failed"] = "Could not save preferences",
        ["CrossPlatform_SearchHistory"] = "Search history",
        ["CrossPlatform_Copy"] = "Copy",
        ["CrossPlatform_Favorite"] = "Favorite",
        ["CrossPlatform_Unfavorite"] = "Unfavorite",
        ["CrossPlatform_Delete"] = "Delete",
        ["CrossPlatform_ClearHistory"] = "Clear all history",
        ["CrossPlatform_StartupEnable"] = "Enable launch at login",
        ["CrossPlatform_StartupDisable"] = "Disable launch at login",
        ["CrossPlatform_Quit"] = "Quit",
        ["CrossPlatform_HistorySubtitle"] = "Clipboard history",
        ["CrossPlatform_Monitoring"] = "Monitoring clipboard. Select an entry and choose Copy to restore it.",
        ["CrossPlatform_QuickCopyStatus"] = "Copied the latest clipboard entry."
    };

    private static readonly IReadOnlyDictionary<string, string> ChineseText = new Dictionary<string, string>
    {
        ["CrossPlatform_Settings"] = "设置",
        ["CrossPlatform_OpenApp"] = "打开 OpenClipboard",
        ["CrossPlatform_Save"] = "保存",
        ["CrossPlatform_Disabled"] = "已禁用",
        ["CrossPlatform_Hotkey_Help"] = "按下组合键进行设置；在“快速复制”中按 Esc 可禁用。",
        ["CrossPlatform_Hotkey_Unsupported"] = "请使用字母、数字或功能键。",
        ["CrossPlatform_Hotkey_Duplicate"] = "两个快捷键不能相同。",
        ["CrossPlatform_SelectHistoryMode"] = "请选择历史记录保留期限。",
        ["CrossPlatform_Save_Failed"] = "无法保存设置",
        ["CrossPlatform_SearchHistory"] = "搜索历史记录",
        ["CrossPlatform_Copy"] = "复制",
        ["CrossPlatform_Favorite"] = "收藏",
        ["CrossPlatform_Unfavorite"] = "取消收藏",
        ["CrossPlatform_Delete"] = "删除",
        ["CrossPlatform_ClearHistory"] = "清空所有历史记录",
        ["CrossPlatform_StartupEnable"] = "启用登录时启动",
        ["CrossPlatform_StartupDisable"] = "禁用登录时启动",
        ["CrossPlatform_Quit"] = "退出",
        ["CrossPlatform_HistorySubtitle"] = "剪贴板历史记录",
        ["CrossPlatform_Monitoring"] = "正在监视剪贴板。选择一条记录并点击“复制”以恢复。",
        ["CrossPlatform_QuickCopyStatus"] = "已复制最近的剪贴板记录。"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LanguageFallbacks =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["de-DE"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "OpenClipboard öffnen",
                ["CrossPlatform_Quit"] = "Beenden",
                ["CrossPlatform_Save"] = "Speichern",
                ["CrossPlatform_Disabled"] = "Deaktiviert",
                ["CrossPlatform_Hotkey_Help"] = "Tastenkombination drücken. Escape deaktiviert die Schnellkopie.",
                ["CrossPlatform_Hotkey_Unsupported"] = "Buchstaben, Zahlen oder Funktionstasten verwenden.",
                ["CrossPlatform_Hotkey_Duplicate"] = "Die Tastenkombinationen müssen unterschiedlich sein.",
                ["CrossPlatform_SelectHistoryMode"] = "Aufbewahrungszeitraum auswählen.",
                ["CrossPlatform_Save_Failed"] = "Einstellungen konnten nicht gespeichert werden",
                ["CrossPlatform_StartupEnable"] = "Beim Anmelden starten",
                ["CrossPlatform_StartupDisable"] = "Autostart deaktivieren",
                ["CrossPlatform_Monitoring"] = "Zwischenablage wird überwacht. Eintrag auswählen und auf Kopieren klicken."
            },
            ["fr-FR"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "Ouvrir OpenClipboard",
                ["CrossPlatform_Quit"] = "Quitter",
                ["CrossPlatform_Save"] = "Enregistrer",
                ["CrossPlatform_Disabled"] = "Désactivé",
                ["CrossPlatform_Hotkey_Help"] = "Appuyez sur une combinaison. Échap désactive la copie rapide.",
                ["CrossPlatform_Hotkey_Unsupported"] = "Utilisez une lettre, un chiffre ou une touche de fonction.",
                ["CrossPlatform_Hotkey_Duplicate"] = "Les deux raccourcis doivent être différents.",
                ["CrossPlatform_SelectHistoryMode"] = "Choisissez la durée de conservation.",
                ["CrossPlatform_Save_Failed"] = "Impossible d'enregistrer les préférences",
                ["CrossPlatform_StartupEnable"] = "Lancer à la connexion",
                ["CrossPlatform_StartupDisable"] = "Désactiver le lancement à la connexion",
                ["CrossPlatform_Monitoring"] = "Surveillance du presse-papiers. Sélectionnez une entrée et cliquez sur Copier."
            },
            ["ja-JP"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "OpenClipboard を開く",
                ["CrossPlatform_Quit"] = "終了",
                ["CrossPlatform_Save"] = "保存",
                ["CrossPlatform_Disabled"] = "無効",
                ["CrossPlatform_Hotkey_Help"] = "キーの組み合わせを押して設定します。Esc でクイックコピーを無効にします。",
                ["CrossPlatform_Hotkey_Unsupported"] = "英字、数字、またはファンクションキーを使用してください。",
                ["CrossPlatform_Hotkey_Duplicate"] = "2 つのショートカットは別々にしてください。",
                ["CrossPlatform_SelectHistoryMode"] = "履歴の保存期間を選択してください。",
                ["CrossPlatform_Save_Failed"] = "設定を保存できませんでした",
                ["CrossPlatform_StartupEnable"] = "ログイン時に起動",
                ["CrossPlatform_StartupDisable"] = "ログイン時の起動を無効化",
                ["CrossPlatform_Monitoring"] = "クリップボードを監視中です。履歴を選択してコピーしてください。"
            },
            ["ko-KR"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "OpenClipboard 열기",
                ["CrossPlatform_Quit"] = "종료",
                ["CrossPlatform_Save"] = "저장",
                ["CrossPlatform_Disabled"] = "사용 안 함",
                ["CrossPlatform_Hotkey_Help"] = "키 조합을 눌러 설정하세요. 빠른 복사는 Esc 키로 끌 수 있습니다.",
                ["CrossPlatform_Hotkey_Unsupported"] = "영문자, 숫자 또는 기능 키를 사용하세요.",
                ["CrossPlatform_Hotkey_Duplicate"] = "두 바로 가기는 서로 달라야 합니다.",
                ["CrossPlatform_SelectHistoryMode"] = "기록 보관 기간을 선택하세요.",
                ["CrossPlatform_Save_Failed"] = "설정을 저장할 수 없습니다",
                ["CrossPlatform_StartupEnable"] = "로그인 시 시작",
                ["CrossPlatform_StartupDisable"] = "로그인 시 시작 끄기",
                ["CrossPlatform_Monitoring"] = "클립보드를 모니터링 중입니다. 항목을 선택하고 복사를 누르세요."
            },
            ["ru-RU"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "Открыть OpenClipboard",
                ["CrossPlatform_Quit"] = "Выход",
                ["CrossPlatform_Save"] = "Сохранить",
                ["CrossPlatform_Disabled"] = "Отключено",
                ["CrossPlatform_Hotkey_Help"] = "Нажмите сочетание клавиш. Esc отключает быстрое копирование.",
                ["CrossPlatform_Hotkey_Unsupported"] = "Используйте букву, цифру или функциональную клавишу.",
                ["CrossPlatform_Hotkey_Duplicate"] = "Сочетания клавиш должны различаться.",
                ["CrossPlatform_SelectHistoryMode"] = "Выберите срок хранения истории.",
                ["CrossPlatform_Save_Failed"] = "Не удалось сохранить настройки",
                ["CrossPlatform_StartupEnable"] = "Запускать при входе в систему",
                ["CrossPlatform_StartupDisable"] = "Отключить запуск при входе",
                ["CrossPlatform_Monitoring"] = "Буфер обмена отслеживается. Выберите запись и нажмите «Копировать»."
            },
            ["es-ES"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "Abrir OpenClipboard",
                ["CrossPlatform_Quit"] = "Salir",
                ["CrossPlatform_Save"] = "Guardar",
                ["CrossPlatform_Disabled"] = "Desactivado",
                ["CrossPlatform_Hotkey_Help"] = "Pulsa una combinación de teclas. Escape desactiva la copia rápida.",
                ["CrossPlatform_Hotkey_Unsupported"] = "Usa una letra, un número o una tecla de función.",
                ["CrossPlatform_Hotkey_Duplicate"] = "Los dos atajos deben ser diferentes.",
                ["CrossPlatform_SelectHistoryMode"] = "Selecciona el periodo de conservación.",
                ["CrossPlatform_Save_Failed"] = "No se pudieron guardar las preferencias",
                ["CrossPlatform_StartupEnable"] = "Iniciar al iniciar sesión",
                ["CrossPlatform_StartupDisable"] = "Desactivar inicio de sesión",
                ["CrossPlatform_Monitoring"] = "Supervisando el portapapeles. Selecciona una entrada y pulsa Copiar."
            },
            ["pt-PT"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "Abrir OpenClipboard",
                ["CrossPlatform_Quit"] = "Sair",
                ["CrossPlatform_Save"] = "Guardar",
                ["CrossPlatform_Disabled"] = "Desativado",
                ["CrossPlatform_Hotkey_Help"] = "Prima uma combinação de teclas. Escape desativa a cópia rápida.",
                ["CrossPlatform_Hotkey_Unsupported"] = "Use uma letra, um número ou uma tecla de função.",
                ["CrossPlatform_Hotkey_Duplicate"] = "Os dois atalhos têm de ser diferentes.",
                ["CrossPlatform_SelectHistoryMode"] = "Selecione o período de retenção.",
                ["CrossPlatform_Save_Failed"] = "Não foi possível guardar as preferências",
                ["CrossPlatform_StartupEnable"] = "Iniciar ao iniciar sessão",
                ["CrossPlatform_StartupDisable"] = "Desativar início de sessão",
                ["CrossPlatform_Monitoring"] = "A monitorizar a área de transferência. Selecione uma entrada e prima Copiar."
            },
            ["th-TH"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "เปิด OpenClipboard",
                ["CrossPlatform_Quit"] = "ออก",
                ["CrossPlatform_Save"] = "บันทึก",
                ["CrossPlatform_Disabled"] = "ปิดใช้งาน",
                ["CrossPlatform_Hotkey_Help"] = "กดชุดปุ่มเพื่อกำหนดค่า กด Escape เพื่อปิดการคัดลอกด่วน",
                ["CrossPlatform_Hotkey_Unsupported"] = "ใช้ตัวอักษร ตัวเลข หรือปุ่มฟังก์ชัน",
                ["CrossPlatform_Hotkey_Duplicate"] = "ปุ่มลัดทั้งสองต้องไม่ซ้ำกัน",
                ["CrossPlatform_SelectHistoryMode"] = "เลือกระยะเวลาการเก็บประวัติ",
                ["CrossPlatform_Save_Failed"] = "บันทึกการตั้งค่าไม่สำเร็จ",
                ["CrossPlatform_StartupEnable"] = "เริ่มทำงานเมื่อเข้าสู่ระบบ",
                ["CrossPlatform_StartupDisable"] = "ปิดการเริ่มทำงานเมื่อเข้าสู่ระบบ",
                ["CrossPlatform_Monitoring"] = "กำลังตรวจสอบคลิปบอร์ด เลือกรายการแล้วคลิกคัดลอก"
            },
            ["vi-VN"] = new Dictionary<string, string>
            {
                ["CrossPlatform_OpenApp"] = "Mở OpenClipboard",
                ["CrossPlatform_Quit"] = "Thoát",
                ["CrossPlatform_Save"] = "Lưu",
                ["CrossPlatform_Disabled"] = "Đã tắt",
                ["CrossPlatform_Hotkey_Help"] = "Nhấn tổ hợp phím để gán. Nhấn Escape để tắt sao chép nhanh.",
                ["CrossPlatform_Hotkey_Unsupported"] = "Dùng chữ cái, chữ số hoặc phím chức năng.",
                ["CrossPlatform_Hotkey_Duplicate"] = "Hai phím tắt phải khác nhau.",
                ["CrossPlatform_SelectHistoryMode"] = "Chọn thời hạn lưu lịch sử.",
                ["CrossPlatform_Save_Failed"] = "Không thể lưu tùy chọn",
                ["CrossPlatform_StartupEnable"] = "Khởi chạy khi đăng nhập",
                ["CrossPlatform_StartupDisable"] = "Tắt khởi chạy khi đăng nhập",
                ["CrossPlatform_Monitoring"] = "Đang theo dõi bảng tạm. Chọn mục rồi nhấn Sao chép."
            }
        };

    private static IReadOnlyDictionary<string, string> LoadDictionary(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Languages", $"language.{language.ToLowerInvariant()}.xaml");
        using var stream = File.OpenRead(path);
        var document = XDocument.Load(stream);
        return document.Root?
            .Elements()
            .Select(element => (Key: element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value, Value: element.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key!, item => item.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
