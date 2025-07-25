﻿using AutoMapper;
using priceapp.Models;
using priceapp.Repositories.Interfaces;
using priceapp.Repositories.Models;
using priceapp.Services.Interfaces;

namespace priceapp.Services.Implementation;

public class PackagesService : IPackagesService
{
    private readonly IMapper _mapper;
    private readonly IPackagesRepository _packagesRepository;

    public PackagesService(IMapper mapper, IPackagesRepository packagesRepository)
    {
        _mapper = mapper;
        _packagesRepository = packagesRepository;
    }

    public async Task<List<PackageModel>> GetPackagesAsync()
    {
        return _mapper.Map<List<PackageModel>>(await _packagesRepository.GetPackagesAsync());
    }

    public async Task InsertPackageAsync(PackageModel model)
    {
        await _packagesRepository.InsertPackageAsync(_mapper.Map<PackageRepositoryModel>(model));
    }

    public async Task UpdatePackageAsync(PackageModel model)
    {
        await _packagesRepository.UpdatePackageAsync(_mapper.Map<PackageRepositoryModel>(model));
    }
}