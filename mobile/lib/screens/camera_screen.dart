import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:image/image.dart' as img;
import 'package:image_picker/image_picker.dart';
import '../models/ordem_servico_model.dart';
import '../services/api_service.dart';
import '../services/photo_validator.dart';
import '../widgets/framing_overlay.dart';
import 'resultado_screen.dart';

class CameraScreen extends StatefulWidget {
  final OrdemServicoModel os;
  final int? unidadeId;
  final String? numeroUnidade;

  const CameraScreen({super.key, required this.os, this.unidadeId, this.numeroUnidade});

  @override
  State<CameraScreen> createState() => _CameraScreenState();
}

class _CameraScreenState extends State<CameraScreen> {
  final _picker = ImagePicker();
  bool _enviando = false;
  bool _fotoborrada = false;
  Map<String, dynamic>? _progresso;
  List<dynamic>? _unidades;

  @override
  void initState() {
    super.initState();
    _carregarProgresso();
    _carregarUnidades();
  }

  Future<void> _carregarProgresso() async {
    try {
      final p = await ApiService.obterProgresso(widget.os.id);
      setState(() => _progresso = p);
    } catch (_) {}
  }

  Future<void> _carregarUnidades() async {
    try {
      final detalhes = await ApiService.obterDetalhesOs(widget.os.id);
      setState(() => _unidades = detalhes['unidades'] as List<dynamic>);
    } catch (_) {}
  }

  Future<void> _tirarFoto() async {
    final foto = await _picker.pickImage(
      source: ImageSource.camera,
      imageQuality: 90,
      maxWidth: 2048,
    );
    if (foto == null) return;

    // Lê bytes via XFile — funciona tanto no mobile quanto no web (blob URL)
    final fotoBytes = await foto.readAsBytes();

    if (fotoBytes.length < 50 * 1024) {
      _mostrarErro('Foto muito pequena ou escura. Tente novamente com melhor iluminação.');
      return;
    }

    // Blur check — a variância do Laplaciano no frame inteiro erra às vezes
    // (fundo com textura pode inflar o score de foto ruim, ver photo_validator.dart).
    // Em vez de rejeitar direto, mostra a foto pro fiscal decidir: o olho humano
    // resolve os casos que o algoritmo não consegue.
    final decoded = img.decodeImage(fotoBytes);
    if (decoded != null && PhotoValidator.calcularNitidez(decoded) < 100) {
      setState(() => _fotoborrada = true);
      final confirmaLegivel = await _confirmarFotoSuspeita(fotoBytes);
      if (confirmaLegivel != true) {
        _mostrarErro('Foto borrada. Tente novamente com a câmera mais estável.');
        return;
      }
    }
    setState(() => _fotoborrada = false);

    setState(() => _enviando = true);
    try {
      final unidadeId = widget.unidadeId ?? await _selecionarUnidade();
      if (unidadeId == null) return;

      final resultado = await ApiService.uploadFoto(widget.os.id, unidadeId, foto);
      if (!mounted) return;

      Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => ResultadoScreen(
            leitura: resultado,
            os: widget.os,
            onProxima: () {
              Navigator.of(context).pop();
              _carregarProgresso();
            },
          ),
        ),
      );
    } catch (e) {
      _mostrarErro(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      setState(() => _enviando = false);
    }
  }

  // Resolve o número digitado pelo fiscal (ex: "101") para o Id interno da
  // unidade no banco — os dois são valores diferentes, não intercambiáveis.
  int? _resolverUnidadePorNumero(String numeroDigitado) {
    final numero = numeroDigitado.trim();
    if (numero.isEmpty || _unidades == null) return null;
    for (final u in _unidades!) {
      if (u['numero'].toString() == numero) return u['id'] as int;
    }
    return null;
  }

  Future<int?> _selecionarUnidade() async {
    // Quando unidade já está definida (fluxo normal)
    if (widget.unidadeId != null) return widget.unidadeId;

    // Mostrar input para o fiscal digitar o número
    final ctrl = TextEditingController();
    return showDialog<int>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Qual unidade?'),
        content: TextField(
          controller: ctrl,
          decoration: const InputDecoration(labelText: 'Número da unidade', hintText: 'Ex: 101'),
          keyboardType: TextInputType.number,
          autofocus: true,
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          ElevatedButton(
            onPressed: () {
              final id = _resolverUnidadePorNumero(ctrl.text);
              if (id == null) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text('Unidade "${ctrl.text}" não encontrada nesta OS.')),
                );
                return;
              }
              Navigator.pop(context, id);
            },
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }

  // Mostra a foto em tamanho grande e deixa o fiscal decidir: se dá pra ler o
  // mostrador mesmo com o alerta de borrão, envia mesmo assim; senão, refaz.
  Future<bool?> _confirmarFotoSuspeita(Uint8List fotoBytes) {
    return showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => AlertDialog(
        title: const Text('Foto pode estar borrada'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Dá pra ler o número do mostrador nesta foto?'),
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 350),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Image.memory(fotoBytes, fit: BoxFit.contain),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Tirar de novo'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Dá pra ler, enviar'),
          ),
        ],
      ),
    );
  }

  void _mostrarErro(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg), backgroundColor: Colors.red.shade700, duration: const Duration(seconds: 4)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final registradas = _progresso?['leiturasRegistradas'] as int? ?? 0;
    final total = _progresso?['totalUnidades'] as int? ?? widget.os.totalUnidades;
    final pct = total > 0 ? registradas / total : 0.0;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.os.condominio, style: const TextStyle(fontSize: 16)),
        backgroundColor: const Color(0xFF1D4ED8),
        foregroundColor: Colors.white,
      ),
      body: Stack(
        children: [
          // Main content
          Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                // Progresso
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text('$registradas / $total leituras', style: const TextStyle(fontWeight: FontWeight.bold)),
                            Text('${(pct * 100).toInt()}%', style: const TextStyle(color: Color(0xFF1D4ED8), fontWeight: FontWeight.bold)),
                          ],
                        ),
                        const SizedBox(height: 8),
                        LinearProgressIndicator(value: pct, backgroundColor: Colors.blue.shade100, color: const Color(0xFF1D4ED8)),
                        const SizedBox(height: 4),
                        Text(
                          '${total - registradas} faltando',
                          style: const TextStyle(color: Colors.grey, fontSize: 12),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 32),
                const Icon(Icons.camera_alt, size: 80, color: Color(0xFF1D4ED8)),
                const SizedBox(height: 16),
                const Text(
                  'Aponte a câmera para o hidrômetro',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.w500),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Certifique-se de boa iluminação e foco no mostrador',
                  style: TextStyle(color: Colors.grey, fontSize: 13),
                  textAlign: TextAlign.center,
                ),
                const Spacer(),
                SizedBox(
                  width: double.infinity,
                  height: 56,
                  child: ElevatedButton.icon(
                    onPressed: _enviando ? null : _tirarFoto,
                    icon: _enviando
                        ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : const Icon(Icons.camera_alt, color: Colors.white),
                    label: Text(
                      _enviando ? 'Processando...' : 'Fotografar Hidrômetro',
                      style: const TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1D4ED8),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
              ],
            ),
          ),
          // Framing overlay — shown when blur was detected on last capture
          if (_fotoborrada)
            IgnorePointer(child: FramingOverlay(isBlurry: _fotoborrada)),
        ],
      ),
    );
  }
}
