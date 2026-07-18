// mobile/tool/calibrate_blur.dart
//
// Ferramenta de calibração do threshold de blur detection.
// Roda a mesma lógica de PhotoValidator.calcularNitidez contra um diretório
// de fotos reais de campo e imprime a pontuação de cada uma, ordenada.
//
// Uso: dart run tool/calibrate_blur.dart <diretorio>
// ignore_for_file: avoid_print
import 'dart:io';
import 'package:image/image.dart' as img;
import 'package:hidrometro_brk/services/photo_validator.dart';

void main(List<String> args) {
  final dirPath = args.isNotEmpty ? args[0] : 'test_photos/condominio_exemplo';
  final dir = Directory(dirPath);
  if (!dir.existsSync()) {
    stderr.writeln('Diretório não encontrado: $dirPath');
    exit(1);
  }

  final files = dir
      .listSync()
      .whereType<File>()
      .where((f) => f.path.toLowerCase().endsWith('.png') ||
          f.path.toLowerCase().endsWith('.jpg') ||
          f.path.toLowerCase().endsWith('.jpeg'))
      .toList()
    ..sort((a, b) => a.path.compareTo(b.path));

  final results = <MapEntry<String, double>>[];

  for (final file in files) {
    final bytes = file.readAsBytesSync();
    final image = img.decodeImage(bytes);
    if (image == null) {
      stderr.writeln('Falha ao decodificar: ${file.path}');
      continue;
    }
    final score = PhotoValidator.calcularNitidez(image);
    results.add(MapEntry(file.path.split(Platform.pathSeparator).last, score));
  }

  results.sort((a, b) => a.value.compareTo(b.value));

  print('Arquivo'.padRight(16) + 'Score (variancia Laplaciano)');
  for (final r in results) {
    print(r.key.padRight(16) + r.value.toStringAsFixed(2));
  }
}
