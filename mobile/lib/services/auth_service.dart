import 'dart:convert';
import 'package:http/http.dart' as http;
import '../config/app_config.dart';
import '../models/usuario_model.dart';

class AuthService {
  static UsuarioModel? _usuario;

  static UsuarioModel? get usuarioAtual => _usuario;
  static String? get token => _usuario?.token;

  static Future<UsuarioModel> login(String email, String senha) async {
    final response = await http
        .post(
          Uri.parse('${AppConfig.apiBaseUrl}/api/auth/login'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode({'email': email, 'senha': senha}),
        )
        .timeout(const Duration(seconds: AppConfig.timeoutSeconds));

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body) as Map<String, dynamic>;
      _usuario = UsuarioModel.fromJson(json);
      return _usuario!;
    } else if (response.statusCode == 401) {
      throw Exception('CPF ou senha inválidos');
    } else {
      throw Exception('Erro ao conectar com o servidor');
    }
  }

  static void logout() {
    _usuario = null;
  }

  static bool get estaAutenticado => _usuario != null && _usuario!.tokenValido;
}
