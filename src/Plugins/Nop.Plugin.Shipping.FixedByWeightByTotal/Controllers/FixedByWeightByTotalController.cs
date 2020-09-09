﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class FixedByWeightByTotalController : BasePluginController
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly FixedByWeightByTotalSettings _fixedByWeightByTotalSettings;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly IMeasureService _measureService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IShippingByWeightByTotalService _shippingByWeightService;
        private readonly IShippingService _shippingService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreService _storeService;
        private readonly MeasureSettings _measureSettings;

        #endregion

        #region Ctor

        public FixedByWeightByTotalController(CurrencySettings currencySettings,
            FixedByWeightByTotalSettings fixedByWeightByTotalSettings,
            ICountryService countryService,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            IMeasureService measureService,
            IPermissionService permissionService,
            ISettingService settingService,
            IShippingByWeightByTotalService shippingByWeightService,
            IShippingService shippingService,
            IStateProvinceService stateProvinceService,
            IStoreService storeService,
            MeasureSettings measureSettings)
        {
            _currencySettings = currencySettings;
            _fixedByWeightByTotalSettings = fixedByWeightByTotalSettings;
            _countryService = countryService;
            _currencyService = currencyService;
            _localizationService = localizationService;
            _measureService = measureService;
            _permissionService = permissionService;
            _settingService = settingService;
            _shippingByWeightService = shippingByWeightService;
            _stateProvinceService = stateProvinceService;
            _shippingService = shippingService;
            _storeService = storeService;
            _measureSettings = measureSettings;
        }

        #endregion

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                LimitMethodsToCreated = _fixedByWeightByTotalSettings.LimitMethodsToCreated,
                ShippingByWeightByTotalEnabled = _fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled
            };

            //stores
            model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var store in await _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });
            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var warehouses in await _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = warehouses.Name, Value = warehouses.Id.ToString() });
            //shipping methods
            foreach (var sm in await _shippingService.GetAllShippingMethods())
                model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString() });
            //countries
            model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
            var countries = await _countryService.GetAllCountries();
            foreach (var c in countries)
                model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
            //states
            model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });

            model.SetGridPageSize();

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            //save settings
            _fixedByWeightByTotalSettings.LimitMethodsToCreated = model.LimitMethodsToCreated;
            await _settingService.SaveSetting(_fixedByWeightByTotalSettings);

            return Json(new { Result = true });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SaveMode(bool value)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            //save settings
            _fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled = value;
            await _settingService.SaveSetting(_fixedByWeightByTotalSettings);

            return Json(new { Result = true });
        }

        #region Fixed rate

        [HttpPost]
        public async Task<IActionResult> FixedShippingRateList(ConfigurationModel searchModel)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedDataTablesJson();

            var shippingMethods = (await _shippingService.GetAllShippingMethods()).ToPagedList(searchModel);

            var gridModel = new FixedRateListModel().PrepareToGrid(searchModel, shippingMethods, () =>
            {
                return shippingMethods.Select(shippingMethod => new FixedRateModel
                {
                    ShippingMethodId = shippingMethod.Id,
                    ShippingMethodName = shippingMethod.Name,
                    Rate = _settingService.GetSettingByKey<decimal>(
                        string.Format(FixedByWeightByTotalDefaults.FixedRateSettingsKey, shippingMethod.Id)).Result,
                    TransitDays = _settingService.GetSettingByKey<int?>(
                        string.Format(FixedByWeightByTotalDefaults.TransitDaysSettingsKey, shippingMethod.Id)).Result
                });
            });

            return Json(gridModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFixedShippingRate(FixedRateModel model)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            await _settingService.SetSetting(string.Format(FixedByWeightByTotalDefaults.FixedRateSettingsKey, model.ShippingMethodId), model.Rate, 0, false);
            await _settingService.SetSetting(string.Format(FixedByWeightByTotalDefaults.TransitDaysSettingsKey, model.ShippingMethodId), model.TransitDays, 0, false);

            await _settingService.ClearCache();

            return new NullJsonResult();
        }

        #endregion

        #region Rate by weight

        [HttpPost]
        public async Task<IActionResult> RateByWeightByTotalList(ConfigurationModel searchModel, ConfigurationModel filter)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedDataTablesJson();

            //var records = _shippingByWeightService.GetAll(command.Page - 1, command.PageSize);
            var records = await _shippingByWeightService.FindRecords(
              pageIndex: searchModel.Page - 1,
              pageSize: searchModel.PageSize,
              storeId: filter.SearchStoreId,
              warehouseId: filter.SearchWarehouseId,
              countryId: filter.SearchCountryId,
              stateProvinceId: filter.SearchStateProvinceId,
              zip: filter.SearchZip,
              shippingMethodId: filter.SearchShippingMethodId,
              weight: null,
              orderSubtotal: null
              );

            var gridModel = new ShippingByWeightByTotalListModel().PrepareToGrid(searchModel, records, () =>
            {
                return records.Select(record =>
                {
                    var model = new ShippingByWeightByTotalModel
                    {
                        Id = record.Id,
                        StoreId = record.StoreId,
                        StoreName = _storeService.GetStoreById(record.StoreId).Result?.Name ?? "*",
                        WarehouseId = record.WarehouseId,
                        WarehouseName = _shippingService.GetWarehouseById(record.WarehouseId).Result?.Name ?? "*",
                        ShippingMethodId = record.ShippingMethodId,
                        ShippingMethodName = _shippingService.GetShippingMethodById(record.ShippingMethodId).Result?.Name ??
                                             "Unavailable",
                        CountryId = record.CountryId,
                        CountryName = _countryService.GetCountryById(record.CountryId).Result?.Name ?? "*",
                        StateProvinceId = record.StateProvinceId,
                        StateProvinceName =
                            _stateProvinceService.GetStateProvinceById(record.StateProvinceId).Result?.Name ?? "*",
                        WeightFrom = record.WeightFrom,
                        WeightTo = record.WeightTo,
                        OrderSubtotalFrom = record.OrderSubtotalFrom,
                        OrderSubtotalTo = record.OrderSubtotalTo,
                        AdditionalFixedCost = record.AdditionalFixedCost,
                        PercentageRateOfSubtotal = record.PercentageRateOfSubtotal,
                        RatePerWeightUnit = record.RatePerWeightUnit,
                        LowerWeightLimit = record.LowerWeightLimit,
                        Zip = !string.IsNullOrEmpty(record.Zip) ? record.Zip : "*"
                    };

                    var htmlSb = new StringBuilder("<div>");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.WeightFrom").Result,
                        model.WeightFrom);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource("Plugins.Shipping.FixedByWeightByTotal.Fields.WeightTo").Result,
                        model.WeightTo);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource(
                            "Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalFrom").Result, model.OrderSubtotalFrom);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource(
                            "Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalTo").Result, model.OrderSubtotalTo);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource(
                            "Plugins.Shipping.FixedByWeightByTotal.Fields.AdditionalFixedCost").Result,
                        model.AdditionalFixedCost);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource(
                            "Plugins.Shipping.FixedByWeightByTotal.Fields.RatePerWeightUnit").Result, model.RatePerWeightUnit);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource(
                            "Plugins.Shipping.FixedByWeightByTotal.Fields.LowerWeightLimit").Result, model.LowerWeightLimit);
                    htmlSb.Append("<br />");
                    htmlSb.AppendFormat("{0}: {1}",
                        _localizationService.GetResource(
                            "Plugins.Shipping.FixedByWeightByTotal.Fields.PercentageRateOfSubtotal").Result,
                        model.PercentageRateOfSubtotal);

                    htmlSb.Append("</div>");
                    model.DataHtml = htmlSb.ToString();

                    return model;
                });
            });

            return Json(gridModel);
        }

        public async Task<IActionResult> AddRateByWeightByTotalPopup()
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var model = new ShippingByWeightByTotalModel
            {
                PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode,
                BaseWeightIn = (await _measureService.GetMeasureWeightById(_measureSettings.BaseWeightId))?.Name,
                WeightTo = 1000000,
                OrderSubtotalTo = 1000000
            };

            var shippingMethods = await _shippingService.GetAllShippingMethods();
            if (!shippingMethods.Any())
                return Content("No shipping methods can be loaded");

            //stores
            model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var store in await _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });
            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var warehouses in await _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = warehouses.Name, Value = warehouses.Id.ToString() });
            //shipping methods
            foreach (var sm in shippingMethods)
                model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString() });
            //countries
            model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
            var countries = await _countryService.GetAllCountries(showHidden: true);
            foreach (var c in countries)
                model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
            //states
            model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/AddRateByWeightByTotalPopup.cshtml", model);
        }
        
        [HttpPost]
        public async Task<IActionResult> AddRateByWeightByTotalPopup(ShippingByWeightByTotalModel model)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();
            
            await _shippingByWeightService.InsertShippingByWeightRecord(new ShippingByWeightByTotalRecord
            {
                StoreId = model.StoreId,
                WarehouseId = model.WarehouseId,
                CountryId = model.CountryId,
                StateProvinceId = model.StateProvinceId,
                Zip = model.Zip == "*" ? null : model.Zip,
                ShippingMethodId = model.ShippingMethodId,
                WeightFrom = model.WeightFrom,
                WeightTo = model.WeightTo,
                OrderSubtotalFrom = model.OrderSubtotalFrom,
                OrderSubtotalTo = model.OrderSubtotalTo,
                AdditionalFixedCost = model.AdditionalFixedCost,
                RatePerWeightUnit = model.RatePerWeightUnit,
                PercentageRateOfSubtotal = model.PercentageRateOfSubtotal,
                LowerWeightLimit = model.LowerWeightLimit,
                TransitDays = model.TransitDays
            });

            ViewBag.RefreshPage = true;

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/AddRateByWeightByTotalPopup.cshtml", model);
        }
        
        public async Task<IActionResult> EditRateByWeightByTotalPopup(int id)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var sbw = await _shippingByWeightService.GetById(id);
            if (sbw == null)
                //no record found with the specified id
                return RedirectToAction("Configure");

            var model = new ShippingByWeightByTotalModel
            {
                Id = sbw.Id,
                StoreId = sbw.StoreId,
                WarehouseId = sbw.WarehouseId,
                CountryId = sbw.CountryId,
                StateProvinceId = sbw.StateProvinceId,
                Zip = sbw.Zip,
                ShippingMethodId = sbw.ShippingMethodId,
                WeightFrom = sbw.WeightFrom,
                WeightTo = sbw.WeightTo,
                OrderSubtotalFrom = sbw.OrderSubtotalFrom,
                OrderSubtotalTo = sbw.OrderSubtotalTo,
                AdditionalFixedCost = sbw.AdditionalFixedCost,
                PercentageRateOfSubtotal = sbw.PercentageRateOfSubtotal,
                RatePerWeightUnit = sbw.RatePerWeightUnit,
                LowerWeightLimit = sbw.LowerWeightLimit,
                PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode,
                BaseWeightIn = (await _measureService.GetMeasureWeightById(_measureSettings.BaseWeightId))?.Name,
                TransitDays = sbw.TransitDays
            };

            var shippingMethods = await _shippingService.GetAllShippingMethods();
            if (!shippingMethods.Any())
                return Content("No shipping methods can be loaded");

            var selectedStore = await _storeService.GetStoreById(sbw.StoreId);
            var selectedWarehouse = await _shippingService.GetWarehouseById(sbw.WarehouseId);
            var selectedShippingMethod = await _shippingService.GetShippingMethodById(sbw.ShippingMethodId);
            var selectedCountry = await _countryService.GetCountryById(sbw.CountryId);
            var selectedState = await _stateProvinceService.GetStateProvinceById(sbw.StateProvinceId);
            //stores
            model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var store in await _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString(), Selected = (selectedStore != null && store.Id == selectedStore.Id) });
            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var warehouse in await _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = warehouse.Name, Value = warehouse.Id.ToString(), Selected = (selectedWarehouse != null && warehouse.Id == selectedWarehouse.Id) });
            //shipping methods
            foreach (var sm in shippingMethods)
                model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString(), Selected = (selectedShippingMethod != null && sm.Id == selectedShippingMethod.Id) });
            //countries
            model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
            var countries = await _countryService.GetAllCountries(showHidden: true);
            foreach (var c in countries)
                model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString(), Selected = (selectedCountry != null && c.Id == selectedCountry.Id) });
            //states
            var states = selectedCountry != null ? (await _stateProvinceService.GetStateProvincesByCountryId(selectedCountry.Id, showHidden: true)).ToList() : new List<StateProvince>();
            model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });
            foreach (var s in states)
                model.AvailableStates.Add(new SelectListItem { Text = s.Name, Value = s.Id.ToString(), Selected = (selectedState != null && s.Id == selectedState.Id) });

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/EditRateByWeightByTotalPopup.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditRateByWeightByTotalPopup(ShippingByWeightByTotalModel model)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var sbw = await _shippingByWeightService.GetById(model.Id);
            if (sbw == null)
                //no record found with the specified id
                return RedirectToAction("Configure");

            sbw.StoreId = model.StoreId;
            sbw.WarehouseId = model.WarehouseId;
            sbw.CountryId = model.CountryId;
            sbw.StateProvinceId = model.StateProvinceId;
            sbw.Zip = model.Zip == "*" ? null : model.Zip;
            sbw.ShippingMethodId = model.ShippingMethodId;
            sbw.WeightFrom = model.WeightFrom;
            sbw.WeightTo = model.WeightTo;
            sbw.OrderSubtotalFrom = model.OrderSubtotalFrom;
            sbw.OrderSubtotalTo = model.OrderSubtotalTo;
            sbw.AdditionalFixedCost = model.AdditionalFixedCost;
            sbw.RatePerWeightUnit = model.RatePerWeightUnit;
            sbw.PercentageRateOfSubtotal = model.PercentageRateOfSubtotal;
            sbw.LowerWeightLimit = model.LowerWeightLimit;
            sbw.TransitDays = model.TransitDays;

            await _shippingByWeightService.UpdateShippingByWeightRecord(sbw);

            ViewBag.RefreshPage = true;

            return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/EditRateByWeightByTotalPopup.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRateByWeightByTotal(int id)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.ManageShippingSettings))
                return Content("Access denied");

            var sbw = await _shippingByWeightService.GetById(id);
            if (sbw != null)
                await _shippingByWeightService.DeleteShippingByWeightRecord(sbw);

            return new NullJsonResult();
        }

        #endregion

        #endregion
    }
}