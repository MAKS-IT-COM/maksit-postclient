using MaksIT.Results;


namespace MaksIT.PostClient.Shared.Auth;


/// <summary>
/// User profile secrets first; falls back to the machine shared store for published mailboxes.
/// Writes go to the user store; when a key is already published, the shared copy is refreshed too.
/// </summary>
public sealed class LayeredSecretStore : ISecretStore {
  private readonly ISecretStore _user;
  private readonly ISecretStore _shared;

  public LayeredSecretStore(ISecretStore user, ISecretStore shared) {
    _user = user ?? throw new ArgumentNullException(nameof(user));
    _shared = shared ?? throw new ArgumentNullException(nameof(shared));
  }

  public Result Put(string key, string secret) {
    var saved = _user.Put(key, secret);
    if (!saved.IsSuccess)
      return saved;
    var published = _shared.Get(key);
    if (published.IsSuccess && !string.IsNullOrWhiteSpace(published.Value))
      return _shared.Put(key, secret);
    return saved;
  }

  public Result<string?> Get(string key) {
    var user = _user.Get(key);
    if (user.IsSuccess && !string.IsNullOrWhiteSpace(user.Value))
      return user;
    return _shared.Get(key);
  }

  public Result Delete(string key) {
    _user.Delete(key);
    _shared.Delete(key);
    return Result.Ok();
  }

  /// <summary>Moves mailbox password and OAuth tokens into the shared store.</summary>
  public Result PublishMailbox(string mailboxId) =>
    SharedSecretStore.PublishMailbox(_user, mailboxId);

  /// <summary>Moves mailbox password and OAuth tokens back into the user store.</summary>
  public Result WithdrawMailbox(string mailboxId) =>
    SharedSecretStore.WithdrawMailbox(_user, mailboxId);
}
