using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.SmartContract.Application;
using AElf.WebApp.Application.Chain;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using FileDescriptorSet = AElf.Runtime.CSharp.FileDescriptorSet;

namespace AElf.WebApp.Application.Contract.Services;

public interface IContractMethodAppService
{
    Task<string[]> GetContractViewMethodListAsync(string address);
    Task<string> GetSystemContractAddressByNameAsync(string contractName);
}

public class ContractMethodAppService : ApplicationService, IContractMethodAppService
{
    private static IContractFileDescriptorSetAppService _contractFileDescriptorSetAppService;
    private static ISmartContractAddressService _smartContractAddressService;
    private readonly IBlockchainService _blockchainService;

    public ILogger<ContractMethodAppService> Logger { get; set; }

    public ContractMethodAppService(IContractFileDescriptorSetAppService contractFileDescriptorSetAppService,
        ISmartContractAddressService smartContractAddressService,
        IBlockchainService blockchainService)
    {
        _contractFileDescriptorSetAppService = contractFileDescriptorSetAppService;
        _smartContractAddressService = smartContractAddressService;
        _blockchainService = blockchainService;
    }

    /// <summary>
    ///     Get the view method list of a contract
    /// </summary>
    /// <param name="address">contract address</param>
    /// <returns></returns>
    public async Task<string[]> GetContractViewMethodListAsync(string address)
    {
        try
        {
            var set = new FileDescriptorSet();
            var fds = await _contractFileDescriptorSetAppService.GetContractFileDescriptorSetAsync(address);
            set.MergeFrom(ByteString.CopyFrom(fds));
            var fdList = FileDescriptor.BuildFromByteStrings(set.File, new ExtensionRegistry
            {
                OptionsExtensions.IsView,
            });
            var viewMethodList =
                (from fd in fdList
                    from service in fd.Services
                    from method in service.Methods
                    where method.GetOptions().GetExtension(OptionsExtensions.IsView)
                    select method.Name).ToList();
            return viewMethodList.ToArray();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Error during GetContractViewMethodListAsync");
            throw new UserFriendlyException(Error.Message[Error.NotFound], Error.NotFound.ToString());
        }
    }

    public async Task<string> GetSystemContractAddressByNameAsync(string contractName)
    {
        var chain = await _blockchainService.GetChainAsync();
        if (!contractName.StartsWith("AElf.ContractNames"))
        {
            contractName = $"AElf.ContractNames.{contractName}";
        }
        var contractNameHash = HashHelper.ComputeFrom(contractName);
        var address = await _smartContractAddressService.GetAddressByContractNameAsync(new ChainContext
        {
            BlockHash = chain.BestChainHash,
            BlockHeight = chain.BestChainHeight,
        }, contractNameHash.ToStorageKey());
        return address.ToBase58();
    }
}