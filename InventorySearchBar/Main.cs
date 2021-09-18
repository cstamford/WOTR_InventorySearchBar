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
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityModManagerNet;
using UniRx;
using UnityEngine.UI;

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
        private InventoryStashPCView m_inventory_stash_view;

        private void Awake()
        {
            m_input_field = transform.Find("FieldPlace/SearchField/SearchBackImage/InputField").GetComponent<TMP_InputField>();
            m_input_button = transform.Find("FieldPlace/SearchField/SearchBackImage/Placeholder").GetComponent<OwlcatButton>();
            m_dropdown_button = transform.Find("FieldPlace/SearchField/SearchBackImage/Dropdown/GenerateButtonPlace").GetComponent<OwlcatButton>();
            m_dropdown_icon = transform.Find("FieldPlace/SearchField/SearchBackImage/Dropdown/GenerateButtonPlace/GenerateButton/Icon").gameObject;
            m_dropdown = transform.Find("FieldPlace/SearchField/SearchBackImage/Dropdown").GetComponent<TMP_Dropdown>();
            m_placeholder = transform.Find("FieldPlace/SearchField/SearchBackImage/Placeholder/Label").GetComponent<TextMeshProUGUI>();

            m_input_field.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().SetText("Enter item name...");

            m_input_field.onValueChanged.AddListener(delegate (string _) { OnEdit(); });
            m_input_field.onEndEdit.AddListener(delegate (string _) { OnEndEdit(); });
            m_input_button.OnLeftClick.AddListener(delegate { OnStartEdit(); });
            m_dropdown_button.OnLeftClick.AddListener(delegate { OnShowDropdown(); });
            m_dropdown.onValueChanged.AddListener(delegate (int idx) { OnSelectDropdown(idx); });

            m_dropdown.ClearOptions();
            List<string> options = Enum.GetNames(typeof(ItemsFilter.FilterType)).ToList();
            options[(int)ItemsFilter.FilterType.NoFilter] = "All";
            options[(int)ItemsFilter.FilterType.NonUsable] = "Non-usable";
            m_dropdown.AddOptions(options);

            List<Image> images = new List<Image>();

            foreach (Transform child in transform.parent.Find("SwitchBar"))
            {
                images.Add(child.Find("Icon")?.GetComponent<Image>());
            }

            m_search_icons = images.ToArray();

            Destroy(transform.parent.Find("SwitchBar").gameObject); // existing filters display
            Destroy(GetComponent<CharGenFeatureSearchPCView>()); // controller from where we stole the search bar
        }

        private void ApplyFilter()
        {
            InventorySearchBar.SearchContents = m_input_field.text;
            m_inventory_stash_view.ViewModel.ItemsFilter.CurrentFilter.SetValueAndForceNotify((ItemsFilter.FilterType)m_dropdown.value);
            InventorySearchBar.SearchContents = null;
        }

        private void OnEnable()
        {
            m_inventory_stash_view = null;
        }

        private void Update()
        {
            if (m_inventory_stash_view == null)
            {
                m_inventory_stash_view = GetComponentInParent(typeof(InventoryStashPCView)) as InventoryStashPCView;
                m_inventory_stash_view.ViewModel.ItemSlotsGroup.CollectionChangedCommand.Subscribe(delegate (bool _) { ApplyFilter(); });
                m_inventory_stash_view.ViewModel.ItemsFilter.CurrentSorter.Subscribe(delegate (ItemsFilter.SorterType _) { ApplyFilter(); });
                UpdatePlaceholder();
            }
        }

        private void OnShowDropdown()
        {
            m_dropdown.Show();
        }

        private void OnSelectDropdown(int idx)
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
            UpdatePlaceholder();
            ApplyFilter();
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
                    search_bar.transform.SetSiblingIndex(1); // below filters, above sorting
                    search_bar.GetComponent<RectTransform>().localScale = new Vector3(0.85f, 0.85f, 1.0f);
                    search_bar.GetComponent<RectTransform>().localPosition = new Vector3(0.0f, 4.0f, 0.0f);
                    search_bar.AddComponent<SearchBoxController>();
                }
            }
        }

        public void OnAreaBeginUnloading()
        { }
    }

    public class InventorySearchBar
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static string SearchContents = null;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            Harmony harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            EventBus.Subscribe(new AreaHandler());
            return true;
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
}
