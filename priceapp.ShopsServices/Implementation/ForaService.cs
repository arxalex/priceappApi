﻿using System.Globalization;
using System.Net;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using priceapp.Models;
using priceapp.Repositories.Interfaces;
using priceapp.Services.Interfaces;
using priceapp.ShopsServices.Interfaces;
using priceapp.ShopsServices.Models;
using priceapp.Utils;
using RestSharp;

namespace priceapp.ShopsServices.Implementation;

public class ForaService : IForaService
{
    private readonly IBrandsService _brandsService;
    private readonly RestClient _client;
    private readonly ICountriesService _countriesService;
    private readonly IItemLinksService _itemLinksService;
    private readonly ILogger<ForaService> _logger;
    private readonly IFilialsRepository _filialsRepository;
    private readonly IMapper _mapper;
    private readonly ICategoryLinksRepository _categoryLinksRepository;
    private readonly ICategoriesService _categoriesService;
    private const int ShopId = 2;

    private List<ItemLinkModel> _itemLinks;
    private DateTime _itemLinksLastUpdatedTime;

    private List<ItemLinkModel> ItemLinks
    {
        get
        {
            if (DateTime.Now - _itemLinksLastUpdatedTime <= TimeSpan.FromMinutes(30)) return _itemLinks;
            _itemLinks = _itemLinksService.GetItemLinksAsync(ShopId).Result;
            _itemLinksLastUpdatedTime = DateTime.Now;

            return _itemLinks;
        }
        set
        {
            _itemLinksLastUpdatedTime = DateTime.Now;
            _itemLinks = value;
        }
    }

    public ForaService(IItemLinksService itemLinksService, IBrandsService brandsService,
        ICountriesService countriesService, ILogger<ForaService> logger, IFilialsRepository filialsRepository,
        IMapper mapper, ICategoryLinksRepository categoryLinksRepository, ICategoriesService categoriesService)
    {
        _itemLinksService = itemLinksService;
        _brandsService = brandsService;
        _countriesService = countriesService;
        _logger = logger;
        _filialsRepository = filialsRepository;
        _mapper = mapper;
        _categoryLinksRepository = categoryLinksRepository;
        _categoriesService = categoriesService;
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.catalog.ecom.silpo.ua/")
        };

        _client = new RestClient(httpClient);
        _itemLinksLastUpdatedTime = DateTime.MinValue;
        _itemLinks = new List<ItemLinkModel>();
    }

    public async Task<List<ItemShopModel>> GetItemsByCategoryAsync(int internalCategoryId, int from, int to,
        int internalFilialId = 310)
    {
        _logger.LogInformation("Start Fora GetItemsByCategoryAsync. internalCategoryId: {InternalCategoryId}", internalCategoryId);

        var json = JsonSerializer.Serialize(new
        {
            data = new
            {
                from,
                to,
                categoryId = internalCategoryId,
                filialId = internalFilialId
            },
            method = "GetSimpleCatalogItems"
        });

        var request = new RestRequest("api/2.0/exec/EcomCatalogGlobal", Method.Post);
        request.AddHeader("Content-Type", "application/json");
        request.AddBody(json, "application/json");

        var response = await _client.ExecuteAsync(request);
        _logger.LogInformation("GetSimpleCatalogItems request send. Response is {ResponseStatusCode}", response.StatusCode);

        if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
            throw new ConnectionAbortedException("Could not get data from Fora");

        var result = JsonSerializer.Deserialize<ForaCatalogItems>(response.Content);
        if (result == null) throw new ConnectionAbortedException("Could not parse data");

        _logger.LogInformation("Data Deserialized. Count is {ItemsCount}", result.items.Count);

        var inTableItems = await _itemLinksService.GetItemLinksAsync(ShopId);
        ItemLinks = inTableItems;
        var notHandledResult = result.items.Where(item => !inTableItems.Exists(x => x.InShopId == item.id)).ToList();
        
        _logger.LogInformation("New items count is {Count}", notHandledResult.Count);

        var items = new List<ItemShopModel>();
        var categories = await _categoriesService.GetCategoriesAsync();
        var categoryLinks = _mapper.Map<List<CategoryLinkModel>>(await _categoryLinksRepository.GetCategoryLinksAsync(ShopId));
        var brands = await _brandsService.GetBrandsAsync();
        var countries = await _countriesService.GetCountriesAsync();

        foreach (var value in notHandledResult)
        {
            var packageObject = value.parameters?.FirstOrDefault(x => x.key == "packageType");
            var packageLabel = packageObject != null ? packageObject.value : "";
            var package = 0;
            var (units, unitShort) = NumericHelper.ParseNumberString(value.unit);
            if (value.parameters?.FirstOrDefault(x => x.key == "isWeighted") != null) package = 1;

            var brandObject = value.parameters?.FirstOrDefault(x => x.key == "trademark");
            var brandLabel = "Без ТМ";
            if (brandObject != null) brandLabel = brandObject.value;

            var calorieObject = value.parameters?.FirstOrDefault(x => x.key == "calorie");
            double? calorie = null;
            if (calorieObject != null)
                calorie = double.Parse(calorieObject.value.Split('/', 2)[0].Replace(',', '.'),
                    CultureInfo.InvariantCulture);

            var carbohydratesObject = value.parameters?.FirstOrDefault(x => x.key == "carbohydrates");
            double? carbohydrates = null;
            if (carbohydratesObject != null)
                carbohydrates = double.Parse(carbohydratesObject.value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var fatsObject = value.parameters?.FirstOrDefault(x => x.key == "fats");
            double? fats = null;
            if (fatsObject != null)
                fats = double.Parse(fatsObject.value.Replace(',', '.'), CultureInfo.InvariantCulture);

            var proteinsObject = value.parameters?.FirstOrDefault(x => x.key == "proteins");
            double? proteins = null;
            if (proteinsObject != null)
                proteins = double.Parse(proteinsObject.value.Replace(',', '.'), CultureInfo.InvariantCulture);

            var alcoholObject = value.parameters?.FirstOrDefault(x => x.key == "alcoholContent");
            double? alcohol = null;
            if (alcoholObject != null)
                alcohol = double.Parse(alcoholObject.value.Replace(',', '.'), CultureInfo.InvariantCulture);

            var countryObject = value.parameters?.FirstOrDefault(x => x.key == "country");
            var country = "";
            if (countryObject != null) country = countryObject.value;

            switch (unitShort)
            {
                case "кг":
                    if (package != 1) package = 5;
                    break;
                case "г":
                    if (package != 1) package = 5;
                    units /= 1000;
                    break;
                case "шт/уп":
                    package = 5;
                    break;
                case "л":
                    package = 6;
                    break;
                case "мл":
                    package = 6;
                    units /= 1000;
                    break;
                case "шт":
                    package = 9;
                    break;
            }

            CategoryModel? categoryModel = null;
            CategoryLinkModel? categoryLinkModel = null;
            try
            {
                categoryLinkModel = categoryLinks.FirstOrDefault(x => x.CategoryShopId == value.categories[^1].id);
                categoryModel = categories.FirstOrDefault(x => x.Id == categoryLinkModel?.CategoryId);
            }
            catch (Exception e)
            {
                _logger.LogInformation(e.Message);
            }

            var brandModel = brandLabel.Length > 0 ? brands.FirstOrDefault(x => x.Label == brandLabel) : null;
            var countryModel = country.Length > 0 ? countries.FirstOrDefault(x => x.Label == country) : null;

            items.Add(new ItemShopModel
            {
                Item = new ItemModel
                {
                    Id = -1,
                    Label = value.name,
                    Image = value.mainImage,
                    Category = categoryModel?.Id ?? 0,
                    Brand = brandModel?.Id ?? 0,
                    Package = package,
                    Units = units ?? 0,
                    Calorie = calorie,
                    Carbohydrates = carbohydrates,
                    Fat = fats,
                    Proteins = proteins,
                    Additional = new
                    {
                        Alcohol = alcohol,
                        Country = countryModel?.Id ?? 0
                    }
                },
                InShopId = value.id,
                Brand = brandLabel,
                Package = packageLabel,
                Country = country,
                Category = categoryLinkModel != null ? categoryLinkModel.ShopCategoryLabel : value.categories[^1].name,
                Url = "https://shop.fora.ua/product/" + value.slug,
                ShopId = ShopId
            });
        }
        
        _logger.LogInformation("End GetItemsByCategoryAsync. Total items {ItemsCount}", items.Count);

        return items;
    }

    public async Task<List<PriceModel>> GetPricesAsync(int categoryId, int internalFilialId, int filialId, int from = 0,
        int to = 10000)
    {
        _logger.LogInformation("Start Fora GetPricesAsync. categoryId: {CategoryId}, filialId: {FilialId}", categoryId, filialId);

        var internalCategories =
            _mapper.Map<List<CategoryLinkModel>>(await _categoryLinksRepository.GetCategoryLinksAsync(ShopId, categoryId));
        var items = new List<ForaItemModel>();
        foreach (var internalCategory in internalCategories)
        {
            var json = JsonSerializer.Serialize(new
            {
                data = new
                {
                    from,
                    to,
                    internalCategory.CategoryShopId,
                    filialId = internalFilialId
                },
                method = "GetSimpleCatalogItems"
            });

            var request = new RestRequest("api/2.0/exec/EcomCatalogGlobal", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddBody(json, "application/json");

            var response = await _client.ExecuteAsync(request);
            _logger.LogInformation("GetSimpleCatalogItems request send. Response is {ResponseStatusCode}", response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                throw new ConnectionAbortedException("Could not get data from Fora");

            var result = JsonSerializer.Deserialize<ForaCatalogItems>(response.Content);
            if (result == null) throw new ConnectionAbortedException("Could not parse data");
            
            _logger.LogInformation("Items deserialized. Count: {ItemsCount}", result.items.Count);

            items.AddRange(result.items);
        }
        
        var prices = (from item in items
            join link in ItemLinks on item.id equals link.InShopId
            select new PriceModel()
            {
                FilialId = filialId,
                Price = item.price,
                Quantity = item.quantity ?? 0,
                PriceFactor = null,
                ShopId = ShopId,
                Id = -1,
                ItemId = link.ItemId
            }).ToList();

        _logger.LogInformation("End Fora GetPricesAsync. Total count {PricesCount}", prices.Count);

        return prices;
    }

    public async Task<List<FilialModel>> GetFilialsAsync()
    {
        _logger.LogInformation("Start Fora GetFilialsAsync");

        var json = JsonSerializer.Serialize(new
        {
            data = new { businessId = 4 },
            method = "GetPickupFilials"
        });

        var request = new RestRequest("api/2.0/exec/EcomCatalogGlobal", Method.Post);
        request.AddHeader("Content-Type", "application/json");
        request.AddBody(json, "application/json");

        var response = await _client.ExecuteAsync(request);
        _logger.LogInformation("GetPickupFilials request send. Response is {ResponseStatusCode}", response.StatusCode);

        if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
            throw new ConnectionAbortedException("Could not get data from Fora");

        var result = JsonSerializer.Deserialize<ForaFilialResponse>(response.Content);
        if (result == null) throw new ConnectionAbortedException("Could not parse data");
        
        _logger.LogInformation("Items deserialized. Count: {ItemsCount}", result.items.Count);

        var inTableItems = _mapper.Map<List<FilialModel>>(await _filialsRepository.GetFilialsAsync(ShopId));
        var notHandledResult =
            result.items.Where(filial => !inTableItems.Exists(x => x.InShopId == filial.id)).ToList();
        
        _logger.LogInformation("New items count is {Count}", notHandledResult.Count);

        var filials = new List<FilialModel>();

        foreach (var filial in notHandledResult)
        {
            var city = StringUtil.ExecuteCityName(filial.city);
            filials.Add(new FilialModel()
            {
                Id = -1,
                City = city,
                House = StringUtil.ExecuteHouseNumber(filial.address),
                Street = StringUtil.ExecuteStreetName(filial.address),
                InShopId = filial.id,
                Label = filial.title,
                Region = await _filialsRepository.GetRegionAsync(city),
                ShopId = ShopId,
                XCord = filial.lon,
                YCord = filial.lat
            });
        }
        
        _logger.LogInformation("End GetFilialsAsync. Total count: {FilialsCount}", filials.Count);

        return filials;
    }

    public async Task<List<CategoryLinkModel>> GetCategoryLinksAsync(int internalFilialId = 310)
    {
        _logger.LogInformation("Start Fora GetCategoryLinksAsync");

        var json = JsonSerializer.Serialize(new
        {
            data = new { filialId = internalFilialId },
            method = "GetCategories"
        });

        var request = new RestRequest("api/2.0/exec/EcomCatalogGlobal", Method.Post);
        request.AddHeader("Content-Type", "application/json");
        request.AddBody(json, "application/json");

        var response = await _client.ExecuteAsync(request);

        _logger.LogInformation("GetCategories request send. Response is {ResponseStatusCode}", response.StatusCode);

        if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
            throw new ConnectionAbortedException("Could not get data from Fora");

        var result = JsonSerializer.Deserialize<ForaCategoriesRequest>(response.Content);
        if (result == null) throw new ConnectionAbortedException("Could not parse data");
        
        _logger.LogInformation("Items deserialized. Count: {ItemsCount}", result.tree.Count);

        var inTableItems =
            _mapper.Map<List<CategoryLinkModel>>(await _categoryLinksRepository.GetCategoryLinksAsync(ShopId));
        var notHandledResult =
            result.tree.Where(categories => !inTableItems.Exists(x => x.CategoryShopId == categories.id)).ToList();
        
        _logger.LogInformation("New items count is {Count}", notHandledResult.Count);
        
        var categoryLinks = notHandledResult.Select(x => new CategoryLinkModel
        {
            Id = -1,
            ShopId = ShopId,
            CategoryId = 0,
            CategoryShopId = x.id,
            ShopCategoryLabel = x.name!
        }).ToList();
        
        _logger.LogInformation("End GetCategoryLinksAsync. Total count: {CategoriesCount}", categoryLinks.Count);

        return categoryLinks;
    }
}