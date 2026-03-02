
namespace DiktaMe.Core.Account;
/// <summary>
/// Combined auth + trial interface. Bridge for migration — new consumers should
/// depend on <see cref="IAccountService"/> or <see cref="ITrialService"/> instead.
/// </summary>
public interface ITrialAccountService : IAccountService, ITrialService
{
    // All members are inherited from IAccountService and ITrialService.
    // This interface exists only as a migration bridge so existing consumers
    // keep compiling while we incrementally migrate them to the split interfaces.
}
