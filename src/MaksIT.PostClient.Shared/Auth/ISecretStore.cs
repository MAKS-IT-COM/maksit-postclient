using MaksIT.Results;


namespace MaksIT.PostClient.Shared.Auth;


public interface ISecretStore {
  Result Put(string key, string secret);

  Result<string?> Get(string key);

  Result Delete(string key);
}
