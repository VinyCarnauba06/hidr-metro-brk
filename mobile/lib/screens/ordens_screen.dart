import 'package:flutter/material.dart';
import '../models/ordem_servico_model.dart';
import '../services/api_service.dart';
import '../services/auth_service.dart';
import '../services/storage_service.dart';
import '../services/sync_service.dart';
import 'camera_screen.dart';
import 'login_screen.dart';

class OrdensScreen extends StatefulWidget {
  const OrdensScreen({super.key});

  @override
  State<OrdensScreen> createState() => _OrdensScreenState();
}

class _OrdensScreenState extends State<OrdensScreen> {
  List<OrdemServicoModel> _ordens = [];
  bool _loading = true;
  String? _erro;
  int _pendentesOffline = 0;

  @override
  void initState() {
    super.initState();
    _carregar();
    // Escutar mudanças no status de sync para atualizar o badge
    SyncService.status.addListener(_onSyncStatusChanged);
  }

  @override
  void dispose() {
    SyncService.status.removeListener(_onSyncStatusChanged);
    super.dispose();
  }

  void _onSyncStatusChanged() {
    // Recarrega contagem de pendentes quando o sync termina
    if (SyncService.status.value != SyncStatus.sincronizando) {
      _contarPendentesOffline();
    }
  }

  Future<void> _carregar() async {
    setState(() { _loading = true; _erro = null; });
    try {
      _ordens = await ApiService.listarOrdensAbertas();
    } catch (e) {
      _erro = e.toString().replaceFirst('Exception: ', '');
    } finally {
      setState(() => _loading = false);
    }
    _contarPendentesOffline();
  }

  Future<void> _contarPendentesOffline() async {
    try {
      final pendentes = await StorageService.listarPendentes();
      if (mounted) {
        setState(() => _pendentesOffline = pendentes.length);
      }
    } catch (_) {
      // SQLite indisponível — não exibe badge
    }
  }

  Future<void> _sincronizarAgora() async {
    if (SyncService.status.value == SyncStatus.sincronizando) return;

    final resultado = await SyncService.sincronizarPendentes();
    if (!mounted) return;

    String msg;
    Color cor;
    switch (resultado) {
      case SyncResultado.sucesso:
        msg = 'Todas as leituras foram sincronizadas!';
        cor = Colors.green;
        break;
      case SyncResultado.semPendentes:
        msg = 'Nenhuma leitura pendente.';
        cor = Colors.blue;
        break;
      case SyncResultado.parcial:
        msg = 'Algumas leituras não puderam ser enviadas. Tente novamente.';
        cor = Colors.orange;
        break;
      case SyncResultado.sessaoExpirada:
        msg = 'Sessão expirada. Faça login novamente.';
        cor = Colors.red;
        break;
      case SyncResultado.semConexao:
        msg = 'Sem conexão. As leituras serão enviadas quando conectar.';
        cor = Colors.orange;
        break;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg), backgroundColor: cor, duration: const Duration(seconds: 3)),
    );

    _contarPendentesOffline();
    _carregar();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Ordens de Serviço'),
        backgroundColor: const Color(0xFF1D4ED8),
        foregroundColor: Colors.white,
        actions: [
          // Badge de pendentes offline
          if (_pendentesOffline > 0)
            Padding(
              padding: const EdgeInsets.only(right: 4),
              child: ValueListenableBuilder<SyncStatus>(
                valueListenable: SyncService.status,
                builder: (_, syncStatus, __) => IconButton(
                  icon: Stack(
                    clipBehavior: Clip.none,
                    children: [
                      Icon(
                        syncStatus == SyncStatus.sincronizando
                            ? Icons.sync
                            : Icons.cloud_upload_outlined,
                      ),
                      Positioned(
                        right: -6,
                        top: -4,
                        child: Container(
                          padding: const EdgeInsets.all(3),
                          decoration: BoxDecoration(
                            color: Colors.orange,
                            borderRadius: BorderRadius.circular(10),
                          ),
                          constraints: const BoxConstraints(minWidth: 18, minHeight: 18),
                          child: Text(
                            '$_pendentesOffline',
                            style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.bold),
                            textAlign: TextAlign.center,
                          ),
                        ),
                      ),
                    ],
                  ),
                  onPressed: _sincronizarAgora,
                  tooltip: '$_pendentesOffline leitura(s) pendente(s) offline',
                ),
              ),
            ),
          IconButton(icon: const Icon(Icons.refresh), onPressed: _carregar),
          IconButton(
            icon: const Icon(Icons.logout),
            onPressed: () {
              AuthService.logout();
              Navigator.of(context).pushReplacement(
                MaterialPageRoute(builder: (_) => const LoginScreen()),
              );
            },
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _erro != null
              ? Center(child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(_erro!, style: const TextStyle(color: Colors.red)),
                    const SizedBox(height: 16),
                    ElevatedButton(onPressed: _carregar, child: const Text('Tentar novamente')),
                  ],
                ))
              : RefreshIndicator(
                  onRefresh: _carregar,
                  child: _ordens.isEmpty
                      ? const Center(child: Text('Nenhuma OS aberta no momento.'))
                      : Column(
                          children: [
                            // Banner de pendentes offline
                            if (_pendentesOffline > 0)
                              Container(
                                width: double.infinity,
                                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                                color: Colors.orange.shade50,
                                child: Row(
                                  children: [
                                    Icon(Icons.cloud_off, size: 18, color: Colors.orange.shade700),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(
                                        '$_pendentesOffline leitura(s) aguardando sincronização',
                                        style: TextStyle(fontSize: 13, color: Colors.orange.shade800, fontWeight: FontWeight.w500),
                                      ),
                                    ),
                                    ValueListenableBuilder<SyncStatus>(
                                      valueListenable: SyncService.status,
                                      builder: (_, status, __) => status == SyncStatus.sincronizando
                                          ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                                          : TextButton(
                                              onPressed: _sincronizarAgora,
                                              child: const Text('Sincronizar', style: TextStyle(fontSize: 12)),
                                            ),
                                    ),
                                  ],
                                ),
                              ),
                            Expanded(
                              child: ListView.builder(
                                padding: const EdgeInsets.all(16),
                                itemCount: _ordens.length,
                                itemBuilder: (ctx, i) => _CardOs(os: _ordens[i]),
                              ),
                            ),
                          ],
                        ),
                ),
    );
  }
}

class _CardOs extends StatelessWidget {
  final OrdemServicoModel os;

  const _CardOs({required this.os});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 2,
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        title: Text(os.condominio, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Competência: ${os.competencia}', style: const TextStyle(fontSize: 13)),
            if (os.endereco != null)
              Text(os.endereco!, style: const TextStyle(color: Colors.grey, fontSize: 12)),
            const SizedBox(height: 4),
            Row(children: [
              const Icon(Icons.home, size: 14, color: Colors.grey),
              const SizedBox(width: 4),
              Text('${os.totalUnidades} unidades', style: const TextStyle(fontSize: 12, color: Colors.grey)),
            ]),
            if (os.dataLimite != null) ...[
              const SizedBox(height: 4),
              Row(children: [
                Icon(
                  os.prazoVencido ? Icons.warning_amber_rounded : Icons.event,
                  size: 14,
                  color: os.prazoVencido ? Colors.red : Colors.grey,
                ),
                const SizedBox(width: 4),
                Text(
                  os.prazoVencido
                      ? 'Prazo vencido: ${_formatarData(os.dataLimite!)}'
                      : 'Prazo: ${_formatarData(os.dataLimite!)}',
                  style: TextStyle(
                    fontSize: 12,
                    color: os.prazoVencido ? Colors.red : Colors.grey,
                    fontWeight: os.prazoVencido ? FontWeight.bold : FontWeight.normal,
                  ),
                ),
              ]),
            ],
          ],
        ),
        trailing: ElevatedButton(
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => CameraScreen(os: os)),
          ),
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF1D4ED8),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
          ),
          child: const Text('Iniciar', style: TextStyle(color: Colors.white)),
        ),
      ),
    );
  }

  String _formatarData(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';
}
