class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'http://10.0.2.2:5000', // localhost no Android emulador
  );

  static const int timeoutSeconds = 30;
  static const int maxTentativasFoto = 3;
  static const double confiancaMinima = 0.80;
}
