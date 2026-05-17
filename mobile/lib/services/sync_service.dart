import 'dart:io';
import 'api_service.dart';
import 'auth_service.dart';
import 'storage_service.dart';

enum SyncResultado {
  sucesso,
  semPendentes,
  parcial,        // alguns itens sincronizados, outros falharam por erro de rede
  sessaoExpirada, // JWT expirou — fila pausada, dados preservados no SQLite
  semConexao,
}

class SyncService {
  // Processa todas as leituras pendentes no SQLite.
  // Garantias:
  //   - Um item só é removido da fila quando sincronizado = 1 (após confirmação da API).
  //   - Erro de rede → incrementa retentativas, preserva o registro.
  //   - JWT 401 → pausa a fila IMEDIATAMENTE, faz logout limpo, retorna sessaoExpirada.
  //     Os dados locais são preservados intactos para reenvio após novo login.
  static Future<SyncResultado> sincronizarPendentes() async {
    if (!AuthService.estaAutenticado) return SyncResultado.sessaoExpirada;

    List<Map<String, dynamic>> pendentes;
    try {
      pendentes = await StorageService.listarPendentesParaSync();
    } catch (_) {
      return SyncResultado.semConexao;
    }

    if (pendentes.isEmpty) return SyncResultado.semPendentes;

    int enviados = 0;
    int falhas = 0;

    for (final item in pendentes) {
      final id = item['id'] as int;

      try {
        await _enviarItem(item);
        await StorageService.marcarSincronizado(id);
        enviados++;
      } on TokenExpiradoException {
        // JWT expirado: pausa a fila e faz logout preservando SQLite.
        // O usuario precisará fazer login novamente; os pendentes serão enviados depois.
        AuthService.logout();
        return SyncResultado.sessaoExpirada;
      } on SocketException {
        await StorageService.marcarErroSync(id, 'sem_conexao');
        falhas++;
        // continua para o próximo item — pode ser que o problema seja transitório
      } catch (e) {
        await StorageService.marcarErroSync(id, e.toString());
        falhas++;
      }
    }

    if (falhas == 0) return SyncResultado.sucesso;
    if (enviados == 0) return SyncResultado.semConexao;
    return SyncResultado.parcial;
  }

  static Future<void> _enviarItem(Map<String, dynamic> item) async {
    final fotoPath = item['foto_path'] as String?;
    final osId = item['os_id'] as int;
    final unidadeId = item['unidade_id'] as int;

    if (fotoPath != null && fotoPath.isNotEmpty) {
      final arquivo = File(fotoPath);
      if (await arquivo.exists()) {
        // Envia foto para o endpoint de upload — a API faz OCR e persiste
        await ApiService.uploadFoto(osId, unidadeId, arquivo);
        return;
      }
    }

    // Sem foto: envia como leitura manual com o valor gravado offline
    final valorM3 = item['valor_m3'] as double?;
    if (valorM3 == null) {
      // Item corrompido — registra erro sem crashar a fila
      throw Exception('Leitura pendente sem foto e sem valor_m3 (id=${ item['id'] })');
    }

    // Recurso manual: precisa de um leituraId — para o caso offline, salva uma
    // leitura "placeholder" via upload sem arquivo. Como alternativa futura, a API
    // pode expor POST /fiscal/leitura/manual-sem-foto. Por ora lança para incrementar
    // retentativas e aguardar que o fiscal sincronize enquanto conectado.
    throw Exception('Foto não encontrada no dispositivo. Reconecte e tire a foto novamente.');
  }
}
