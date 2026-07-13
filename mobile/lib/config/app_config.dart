class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'http://localhost:5000', // web dev; Android emulador usa 10.0.2.2
  );

  static const int timeoutSeconds = 30;
  static const int maxTentativasFoto = 3;
  static const double confiancaMinima = 0.80;
}
