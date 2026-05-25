import 'package:flutter/material.dart';
import '../models/leitura_model.dart';
import '../models/ordem_servico_model.dart';
import '../services/api_service.dart';

class ResultadoScreen extends StatelessWidget {
  final LeituraModel leitura;
  final OrdemServicoModel os;
  final VoidCallback onProxima;

  const ResultadoScreen({
    super.key,
    required this.leitura,
    required this.os,
    required this.onProxima,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Resultado'),
        backgroundColor: leitura.sucesso ? const Color(0xFF16A34A) : const Color(0xFFDC2626),
        foregroundColor: Colors.white,
        automaticallyImplyLeading: false,
      ),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          children: [
            Icon(
              leitura.sucesso ? Icons.check_circle : Icons.warning_rounded,
              size: 80,
              color: leitura.sucesso ? const Color(0xFF16A34A) : const Color(0xFFF59E0B),
            ),
            const SizedBox(height: 16),
            Text(
              leitura.sucesso ? 'Leitura Registrada!' : 'Foto Ilegível',
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.bold,
                color: leitura.sucesso ? const Color(0xFF16A34A) : const Color(0xFFF59E0B),
              ),
            ),
            const SizedBox(height: 8),
            Text('Unidade ${leitura.numeroUnidade}', style: const TextStyle(fontSize: 16, color: Colors.grey)),
            const SizedBox(height: 24),

            if (leitura.sucesso) ...[
              _InfoCard('Valor Lido', leitura.valorFormatado, Icons.speed),
              const SizedBox(height: 8),
              _InfoCard('Confiança da IA', '${leitura.confiancaPercent}%', Icons.psychology),
              const SizedBox(height: 8),
              _InfoCard('Origem', leitura.origem, Icons.info_outline),
              if (leitura.suspeitaVazamento) ...[
                const SizedBox(height: 16),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.red.shade50,
                    border: Border.all(color: Colors.red.shade200),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Row(
                    children: [
                      Icon(Icons.water_drop, color: Colors.red),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Suspeita de vazamento detectada. O operador será notificado.',
                          style: TextStyle(color: Colors.red, fontWeight: FontWeight.w500),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ] else ...[
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: Colors.orange.shade50, borderRadius: BorderRadius.circular(8)),
                child: Text(leitura.motivo ?? 'Não foi possível extrair a leitura',
                    style: const TextStyle(color: Colors.orange), textAlign: TextAlign.center),
              ),
            ],

            const Spacer(),

            if (!leitura.sucesso && leitura.permiteRecurso) ...[
              SizedBox(
                width: double.infinity,
                height: 48,
                child: OutlinedButton.icon(
                  onPressed: () => _abrirRecursoManual(context),
                  icon: const Icon(Icons.edit),
                  label: const Text('Digitar Manualmente'),
                  style: OutlinedButton.styleFrom(
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
              const SizedBox(height: 12),
            ],

            SizedBox(
              width: double.infinity,
              height: 52,
              child: ElevatedButton.icon(
                onPressed: onProxima,
                icon: const Icon(Icons.arrow_forward, color: Colors.white),
                label: const Text('Próxima Unidade', style: TextStyle(color: Colors.white, fontSize: 16)),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF1D4ED8),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _abrirRecursoManual(BuildContext context) {
    final m3Ctrl = TextEditingController();
    final litrosCtrl = TextEditingController(text: '0');
    final obsCtrl = TextEditingController();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (ctx) => Padding(
        padding: EdgeInsets.only(
          left: 24, right: 24, top: 24,
          bottom: MediaQuery.of(ctx).viewInsets.bottom + 24,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Digitar Leitura Manual', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 16),
            TextField(
              controller: m3Ctrl,
              decoration: const InputDecoration(labelText: 'Metros cúbicos (m³)', hintText: 'Ex: 1234', border: OutlineInputBorder()),
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              autofocus: true,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: litrosCtrl,
              decoration: const InputDecoration(labelText: 'Litros', hintText: 'Ex: 567', border: OutlineInputBorder()),
              keyboardType: TextInputType.number,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: obsCtrl,
              decoration: const InputDecoration(labelText: 'Observação (opcional)', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => Navigator.pop(ctx),
                    child: const Text('Cancelar'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton(
                    onPressed: () async {
                      final m3 = double.tryParse(m3Ctrl.text);
                      final litros = int.tryParse(litrosCtrl.text) ?? 0;
                      if (m3 == null || m3 <= 0) return;

                      try {
                        await ApiService.registrarManual(
                          leitura.id,
                          valorM3: m3,
                          valorLitros: litros,
                          observacao: obsCtrl.text.isNotEmpty ? obsCtrl.text : null,
                        );
                        if (ctx.mounted) Navigator.pop(ctx);
                        onProxima();
                      } catch (e) {
                        if (ctx.mounted) {
                          ScaffoldMessenger.of(ctx).showSnackBar(
                            SnackBar(content: Text(e.toString()), backgroundColor: Colors.red),
                          );
                        }
                      }
                    },
                    style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF1D4ED8)),
                    child: const Text('Salvar', style: TextStyle(color: Colors.white)),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;

  const _InfoCard(this.label, this.value, this.icon);

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.blue.shade50,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Colors.blue.shade100),
      ),
      child: Row(
        children: [
          Icon(icon, color: const Color(0xFF1D4ED8)),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: const TextStyle(color: Colors.grey, fontSize: 12)),
              Text(value, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
            ],
          ),
        ],
      ),
    );
  }
}
