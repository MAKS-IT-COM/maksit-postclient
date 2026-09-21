namespace MaksIT.PostClient.Shared.Search;


public static class EmbeddingVector {
  public static byte[] ToBlob(float[] values) {
    ArgumentNullException.ThrowIfNull(values);
    var bytes = new byte[values.Length * sizeof(float)];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
  }

  public static float[] FromBlob(byte[] bytes) {
    ArgumentNullException.ThrowIfNull(bytes);
    if (bytes.Length < sizeof(float) || bytes.Length % sizeof(float) != 0)
      return [];
    var values = new float[bytes.Length / sizeof(float)];
    Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
    return values;
  }

  public static float[] Truncate(float[] values, int dimensions) {
    ArgumentNullException.ThrowIfNull(values);
    if (dimensions <= 0 || values.Length <= dimensions)
      return Normalize(values);
    var slice = new float[dimensions];
    Array.Copy(values, slice, dimensions);
    return Normalize(slice);
  }

  public static float[] Normalize(float[] values) {
    ArgumentNullException.ThrowIfNull(values);
    double sum = 0;
    foreach (var value in values)
      sum += value * (double)value;
    if (sum <= 0)
      return values;
    var scale = (float)(1.0 / Math.Sqrt(sum));
    var result = new float[values.Length];
    for (var i = 0; i < values.Length; i++)
      result[i] = values[i] * scale;
    return result;
  }

  public static float Cosine(float[] left, float[] right) {
    ArgumentNullException.ThrowIfNull(left);
    ArgumentNullException.ThrowIfNull(right);
    var n = Math.Min(left.Length, right.Length);
    if (n == 0)
      return 0;
    double sum = 0;
    for (var i = 0; i < n; i++)
      sum += left[i] * (double)right[i];
    return (float)sum;
  }
}
