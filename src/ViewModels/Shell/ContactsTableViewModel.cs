using System.Collections.ObjectModel;
using CbsContractsDesktopClient.Models.Contacts;

namespace CbsContractsDesktopClient.ViewModels.Shell
{
    public sealed class ContactsTableViewModel
    {
        public ObservableCollection<ContactTableRow> Contacts { get; } =
        [
            new()
            {
                Id = 1001,
                FullName = "РђРЅРЅР° РљРѕРІР°Р»РµРІР°",
                CompanyName = "РћРћРћ РђР»СЊС„Р° РџРѕСЃС‚Р°РІРєР°",
                DepartmentName = "Р—Р°РєСѓРїРєРё",
                Email = "a.kovaleva@alpha.example",
                Status = "РђРєС‚РёРІРµРЅ"
            },
            new()
            {
                Id = 1002,
                FullName = "РЎРµСЂРіРµР№ РР»СЊРёРЅ",
                CompanyName = "РђРћ РўРµС…РРјРїРѕСЂС‚",
                DepartmentName = "Р®СЂРёРґРёС‡РµСЃРєРёР№ РѕС‚РґРµР»",
                Email = "s.ilin@techimport.example",
                Status = "РќР° СЃРѕРіР»Р°СЃРѕРІР°РЅРёРё"
            },
            new()
            {
                Id = 1003,
                FullName = "РњР°СЂРёСЏ РЎРѕРєРѕР»РѕРІР°",
                CompanyName = "РћРћРћ РЎРµРІРµСЂР›РѕРіРёСЃС‚РёРє",
                DepartmentName = "Р›РѕРіРёСЃС‚РёРєР°",
                Email = "m.sokolova@northlog.example",
                Status = "РђРєС‚РёРІРµРЅ"
            },
            new()
            {
                Id = 1004,
                FullName = "РРіРѕСЂСЊ Р‘РµР»СЏРµРІ",
                CompanyName = "РџРђРћ Р РµРіРёРѕРЅР­РЅРµСЂРіРѕ",
                DepartmentName = "Р¤РёРЅР°РЅСЃС‹",
                Email = "i.belyaev@regenergy.example",
                Status = "Р—Р°Р±Р»РѕРєРёСЂРѕРІР°РЅ"
            },
            new()
            {
                Id = 1005,
                FullName = "Р•РєР°С‚РµСЂРёРЅР° РњРёСЂРѕРЅРѕРІР°",
                CompanyName = "РћРћРћ РљРѕРЅС‚СЂР°РєС‚РЎРµСЂРІРёСЃ",
                DepartmentName = "РџСЂРѕРґР°Р¶Рё",
                Email = "e.mironova@contracts.example",
                Status = "РђРєС‚РёРІРµРЅ"
            }
        ];
    }
}


