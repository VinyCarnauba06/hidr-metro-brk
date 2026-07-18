import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:image_picker/image_picker.dart';
import '../config/app_config.dart';
import '../models/leitura_model.dart';
import '../models/ordem_servico_model.dart';
import 'auth_service.dart';

class ApiService {
  static Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        if (AuthService.token != null) 'Authorization': 'Bearer ${AuthService.token}',
      };

  static Future<List<OrdemServicoModel>> listarOrdensAbertas() async {
    final resp = await http
        .get(Uri.parse('${AppConfig.apiBaseUrl}/api/fiscal/ordens-abertas'), headers: _headers)
        .timeout(const Duration(seconds: AppConfig.timeoutSeconds));

    _verificarResposta(resp);
    final list = jsonDecode(resp.body) as List;
    return list.map((j) => OrdemServicoModel.fromJson(j as Map<String, dynamic>)).toList();
  }

  static Future<Map<String, dynamic>> obterProgresso(int osId) async {
    final resp = await http
        .get(Uri.parse('${AppConfig.apiBaseUrl}/api/fiscal/os/$osId/progresso'), headers: _headers)
        .timeout(const Duration(seconds: AppConfig.timeoutSeconds));

    _verificarResposta(resp);
    return jsonDecode(resp.body) as Map<String, dynamic>;
  }

  static Future<Map<String, dynamic>> obterDetalhesOs(int osId) async {
    final resp = await http
        .get(Uri.parse('${AppConfig.apiBaseUrl}/api/fiscal/os/$osId'), headers: _headers)
        .timeout(const Duration(seconds: AppConfig.timeoutSeconds));

    _verificarResposta(resp);
    return jsonDecode(resp.body) as Map<String, dynamic>;
  }

  static Future<LeituraModel> uploadFoto(int osId, int unidadeId, XFile foto) async {
    final bytes = await foto.readAsBytes();
    final request = http.MultipartRequest(
      'POST',
      Uri.parse('${AppConfig.apiBaseUrl}/api/fiscal/leitura/upload'),
    )
      ..headers['Authorization'] = 'Bearer ${AuthService.token}'
      ..fields['osId'] = osId.toString()
      ..fields['unidadeId'] = unidadeId.toString()
      ..files.add(http.MultipartFile.fromBytes('foto', bytes, filename: foto.name));

    final streamResp = await request.send().timeout(const Duration(seconds: 60));
    final resp = await http.Response.fromStream(streamResp);

    _verificarResposta(resp);
    return LeituraModel.fromJson(jsonDecode(resp.body) as Map<String, dynamic>);
  }

  static Future<LeituraModel> registrarManual(
    int leituraId, {
    required double valorM3,
    required int valorLitros,
    String? observacao,
  }) async {
    final resp = await http
        .post(
          Uri.parse('${AppConfig.apiBaseUrl}/api/fiscal/leitura/$leituraId/recurso'),
          headers: _headers,
          body: jsonEncode({
            'valorM3': valorM3,
            'valorLitros': valorLitros,
            'observacao': observacao,
          }),
        )
        .timeout(const Duration(seconds: AppConfig.timeoutSeconds));

    _verificarResposta(resp);
    return LeituraModel.fromJson(jsonDecode(resp.body) as Map<String, dynamic>);
  }

  static void _verificarResposta(http.Response resp) {
    // Exceção tipada para 401 — permite que SyncService detecte expiração de JWT
    // sem fazer parse de mensagem de string.
    if (resp.statusCode == 401) throw const TokenExpiradoException();
    if (resp.statusCode >= 400) {
      final body = jsonDecode(resp.body) as Map<String, dynamic>?;
      throw Exception(body?['message'] ?? 'Erro ${resp.statusCode}');
    }
  }
}

// Exceção tipada para JWT expirado — diferencia de erros de rede ou 4xx genéricos.
class TokenExpiradoException implements Exception {
  const TokenExpiradoException();

  @override
  String toString() => 'Sessão expirada. Faça login novamente.';
}
