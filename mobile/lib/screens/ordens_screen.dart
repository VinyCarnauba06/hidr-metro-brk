import 'package:flutter/material.dart';
import '../models/ordem_servico_model.dart';
import '../services/api_service.dart';
import '../services/auth_service.dart';
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

  @override
  void initState() {
    super.initState();
    _carregar();
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
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Ordens de Serviço'),
        backgroundColor: const Color(0xFF1D4ED8),
        foregroundColor: Colors.white,
        actions: [
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
                      : ListView.builder(
                          padding: const EdgeInsets.all(16),
                          itemCount: _ordens.length,
                          itemBuilder: (ctx, i) => _CardOs(os: _ordens[i]),
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
}
