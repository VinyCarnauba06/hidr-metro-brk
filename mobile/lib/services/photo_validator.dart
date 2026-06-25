// mobile/lib/services/photo_validator.dart
import 'package:image/image.dart' as img;

class PhotoValidator {
  // Returns Laplacian variance as a sharpness metric.
  // Threshold: variance < 100 → image is blurry.
  static double calcularNitidez(img.Image imagem) {
    // Downsample first for performance (~256px wide)
    final amostra = img.copyResize(imagem, width: 256);
    final gray = img.grayscale(amostra);

    final w = gray.width;
    final h = gray.height;
    final laplacians = <double>[];

    for (int y = 1; y < h - 1; y++) {
      for (int x = 1; x < w - 1; x++) {
        final center = gray.getPixel(x, y).r.toDouble();
        final top    = gray.getPixel(x, y - 1).r.toDouble();
        final bottom = gray.getPixel(x, y + 1).r.toDouble();
        final left   = gray.getPixel(x - 1, y).r.toDouble();
        final right  = gray.getPixel(x + 1, y).r.toDouble();
        laplacians.add((top + bottom + left + right - 4 * center).abs());
      }
    }

    if (laplacians.isEmpty) return 0;

    final mean = laplacians.reduce((a, b) => a + b) / laplacians.length;
    return laplacians
            .map((v) => (v - mean) * (v - mean))
            .reduce((a, b) => a + b) /
        laplacians.length;
  }
}
