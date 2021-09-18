using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.FeatureSelector;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Inventory;
using Owlcat.Runtime.UI.Controls.Button;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityModManagerNet;
using UniRx;
using UnityEngine.UI;
using Kingmaker.UI.MVVM._PCView.Vendor;
using Kingmaker.UI.MVVM._VM.Vendor;
using Kingmaker.Blueprints.Root;

namespace InventorySearchBar
{
    public class SearchBoxController : MonoBehaviour
    {
        private TMP_InputField m_input_field;
        private OwlcatButton m_input_button;
        private OwlcatButton m_dropdown_button;
        private GameObject m_dropdown_icon;
        private TMP_Dropdown m_dropdown;
        private TextMeshProUGUI m_placeholder;
        private Image[] m_search_icons;
        private ReactiveProperty<ItemsFilter.FilterType> m_active_filter;

        private void Awake()
        {
            // -- Find all of our fields

            m_input_field = transform.Find("FieldPlace/SearchField/SearchBackImage/InputField").GetComponent<TMP_InputField>();
            m_input_button = transform.Find("FieldPlace/SearchField/SearchBackImage/Placeholder").GetComponent<OwlcatButton>();
            m_dropdown_button = transform.Find("FieldPlace/SearchField/SearchBackImage/Dropdown/GenerateButtonPlace").GetComponent<OwlcatButton>();
            m_dropdown_icon = transform.Find("FieldPlace/SearchField/SearchBackImage/Dropdown/GenerateButtonPlace/GenerateButton/Icon").gameObject;
            m_dropdown = transform.Find("FieldPlace/SearchField/SearchBackImage/Dropdown").GetComponent<TMP_Dropdown>();
            m_placeholder = transform.Find("FieldPlace/SearchField/SearchBackImage/Placeholder/Label").GetComponent<TextMeshProUGUI>();
            m_input_field.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().SetText("Enter item name...");

            // -- Setup handlers

            m_input_field.onValueChanged.AddListener(delegate (string _) { OnEdit(); });
            m_input_field.onEndEdit.AddListener(delegate (string _) { OnEndEdit(); });
            m_input_button.OnLeftClick.AddListener(delegate { OnStartEdit(); });
            m_dropdown_button.OnLeftClick.AddListener(delegate { OnShowDropdown(); });
            m_dropdown.onValueChanged.AddListener(delegate (int _) { OnSelectDropdown(); });

            // -- Dropdown options

            m_dropdown.ClearOptions();

            List<string> options = new List<string>();
            foreach (ItemsFilter.FilterType filter in Enum.GetValues(typeof(ItemsFilter.FilterType)))
            {
                options.Add(LocalizedTexts.Instance.ItemsFilter.GetText(filter));
            }

            // For whatever reason, the localization DB has the wrong info for some of these options... I suspect someone changed the enum order
            // around and these particular strings are not used anywhere.
            options[(int)ItemsFilter.FilterType.Ingredients] = LocalizedTexts.Instance.ItemsFilter.GetText(ItemsFilter.FilterType.NonUsable);
            options[(int)ItemsFilter.FilterType.Usable] = LocalizedTexts.Instance.ItemsFilter.GetText(ItemsFilter.FilterType.Ingredients);
            options[(int)ItemsFilter.FilterType.NonUsable] = LocalizedTexts.Instance.ItemsFilter.GetText(ItemsFilter.FilterType.Usable);

            m_dropdown.AddOptions(options);

            // -- Dropdown images

            GameObject switch_bar = transform.parent.Find("SwitchBar").gameObject;
            List<Image> images = new List<Image>();

            foreach (Transform child in switch_bar.transform)
            {
                images.Add(child.Find("Icon")?.GetComponent<Image>());
            }

            m_search_icons = images.ToArray();

            // -- Positioning

            RectTransform our_transform = GetComponent<RectTransform>();

            if (InventorySearchBar.Settings.EnableCategoryButtons)
            {
                our_transform.localScale = new Vector3(0.6f, 0.6f, 1.0f);
                our_transform.localPosition = new Vector3(0.0f, -8.0f, 0.0f);

                RectTransform their_transform = switch_bar.GetComponent<RectTransform>();
                their_transform.localPosition = new Vector3(
                    their_transform.localPosition.x,
                    their_transform.localPosition.y + 23.0f,
                    their_transform.localPosition.z);
                their_transform.localScale = new Vector3(0.6f, 0.6f, 1.0f);

                Destroy(transform.Find("Background/Decoration/TopLineImage").gameObject);
            }
            else
            {
                our_transform.localScale = new Vector3(0.85f, 0.85f, 1.0f);
                our_transform.localPosition = new Vector3(0.0f, 2.0f, 0.0f);
                Destroy(switch_bar);
            }

            Destroy(GetComponent<CharGenFeatureSearchPCView>()); // controller from where we stole the search bar
        }

        private void ApplyFilter()
        {
            if (InventorySearchBar.SearchContents == null)
            {
                InventorySearchBar.SearchContents = m_input_field.text;
                m_active_filter.SetValueAndForceNotify((ItemsFilter.FilterType)m_dropdown.value);
                InventorySearchBar.SearchContents = null;
            }
        }

        private void OnEnable()
        {
            m_active_filter = null;
        }

        private void Update()
        {
            if (m_active_filter == null)
            {
                if (transform.parent.parent.parent.name == "VendorBlock")
                {
                    VendorPCView vendor_pc_view = GetComponentInParent(typeof(VendorPCView)) as VendorPCView;
                    m_active_filter = vendor_pc_view.ViewModel.VendorItemsFilter.CurrentFilter;
                    vendor_pc_view.ViewModel.VendorSlotsGroup.CollectionChangedCommand.Subscribe(delegate (bool _) { ApplyFilter(); });
                    vendor_pc_view.ViewModel.VendorItemsFilter.CurrentFilter.Subscribe(delegate (ItemsFilter.FilterType filter) { m_dropdown.value = (int)filter; });
                    vendor_pc_view.ViewModel.VendorItemsFilter.CurrentSorter.Subscribe(delegate (ItemsFilter.SorterType _) { ApplyFilter(); });
                }
                else
                {
                    InventoryStashPCView stash_pc_view = GetComponentInParent(typeof(InventoryStashPCView)) as InventoryStashPCView;
                    m_active_filter = stash_pc_view.ViewModel.ItemsFilter.CurrentFilter;
                    stash_pc_view.ViewModel.ItemSlotsGroup.CollectionChangedCommand.Subscribe(delegate (bool _) { ApplyFilter(); });
                    stash_pc_view.ViewModel.ItemsFilter.CurrentFilter.Subscribe(delegate (ItemsFilter.FilterType filter) { m_dropdown.value = (int)filter; });
                    stash_pc_view.ViewModel.ItemsFilter.CurrentSorter.Subscribe(delegate (ItemsFilter.SorterType _) { ApplyFilter(); });

                }

                UpdatePlaceholder();
            }
        }

        private void OnShowDropdown()
        {
            m_dropdown.Show();
        }

        private void OnSelectDropdown()
        {
            UpdatePlaceholder();
            ApplyFilter();
        }

        private void OnStartEdit()
        {
            m_input_button.gameObject.SetActive(false);
            m_input_field.gameObject.SetActive(true);
            m_input_field.Select();
            m_input_field.ActivateInputField();
        }

        private void OnEdit()
        {
            UpdatePlaceholder();
            ApplyFilter();
        }

        private void OnEndEdit()
        {
            m_input_field.gameObject.SetActive(false);
            m_input_button.gameObject.SetActive(true);
            EventSystem.current.SetSelectedGameObject(null); // return focus to regular UI
        }

        private void UpdatePlaceholder()
        {
            m_dropdown_icon.GetComponent<Image>().sprite = m_search_icons[m_dropdown.value]?.sprite;
            m_dropdown_icon.gameObject.SetActive(m_dropdown_icon.GetComponent<Image>().sprite != null);
            m_placeholder.text = string.IsNullOrEmpty(m_input_field.text) ? m_dropdown.options[m_dropdown.value].text : m_input_field.text;
        }
    }

    public class AreaHandler : IAreaHandler
    {
        public void OnAreaDidLoad()
        { 
            if (!InventorySearchBar.Enabled)
            {
                return;
            }

            Transform prefab_transform = Game.Instance.UI.MainCanvas.transform.Find("ChargenPCView/ContentWrapper/DetailedViewZone/ChargenFeaturesDetailedPCView/FeatureSelectorPlace/FeatureSelectorView/FeatureSearchView");

            if (prefab_transform == null)
            {
                InventorySearchBar.Logger.Error("Error: Unable to locate search bar prefab, it's likely a patch has changed the UI setup, or you are in an unexpected situation. Please report this bug!");
                return;
            }

            string[] paths = new string[]
            {
                "ServiceWindowsPCView/InventoryView/Inventory/Stash/StashContainer/PC_FilterBlock/Filters", // in game inventory
                "VendorPCView/MainContent/PlayerStash/PC_FilterBlock/Filters", // vendor - PC side
                "VendorPCView/MainContent/VendorBlock/PC_FilterBlock/Filters", // vendor - vendor side
                "ServiceWindowsConfig/InventoryView/Inventory/Stash/StashContainer/PC_FilterBlock/Filters", // world map inventory
            };

            foreach (string path in paths)
            {
                Transform filters_block_transform = Game.Instance.UI.MainCanvas.transform.Find(path);

                if (filters_block_transform != null)
                {
                    GameObject filters_block = filters_block_transform.gameObject;
                    GameObject search_bar = GameObject.Instantiate(prefab_transform.gameObject);
                    search_bar.name = "CustomSearchBar";
                    search_bar.transform.SetParent(filters_block.transform, false);
                    search_bar.AddComponent<SearchBoxController>();
                }
            }
        }

        public void OnAreaBeginUnloading()
        { }
    }

    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableCategoryButtons = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public class InventorySearchBar
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Settings Settings;
        public static bool Enabled;

        public static string SearchContents = null;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            Settings = Settings.Load<Settings>(modEntry);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            Harmony harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            EventBus.Subscribe(new AreaHandler());

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            Settings.EnableCategoryButtons = GUILayout.Toggle(Settings.EnableCategoryButtons, " EXPERIMENTAL: Enable category toggles in addition to the search bar");
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }

    [HarmonyPatch(typeof(ItemsFilter), nameof(ItemsFilter.ShouldShowItem), new Type[] { typeof(BlueprintItem), typeof(ItemsFilter.FilterType) })]
    public static class ItemsFilter_ShouldShowItem
    {
        [HarmonyPostfix]
        public static void Postfix(BlueprintItem blueprintItem, ref bool __result)
        { 
            if (!string.IsNullOrWhiteSpace(InventorySearchBar.SearchContents))
            {
                __result = __result && (
                    blueprintItem.Name.IndexOf(InventorySearchBar.SearchContents, StringComparison.OrdinalIgnoreCase) >= 0 || // name match
                    blueprintItem.SubtypeName.IndexOf(InventorySearchBar.SearchContents, StringComparison.OrdinalIgnoreCase) >= 0); // type match
            }
        }
    }

    [HarmonyPatch(typeof(VendorVM), nameof(VendorVM.UpdateVendorSide))]
    public static class VendorVM_UpdateVendorSide
    {
        [HarmonyPostfix]
        public static void Postfix(VendorVM __instance)
        {
            // For some reason the vendor slots group changed notification is not dispatched, it is handled immediately, so we dispatch it here.
            // This allows the vendor UI to properly update with the filter.
            __instance.VendorSlotsGroup.CollectionChangedCommand.Execute(false);
        }
    }
}
