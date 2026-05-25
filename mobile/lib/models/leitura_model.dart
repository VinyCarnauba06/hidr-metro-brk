class LeituraModel {
  final int id;
  final int unidadeId;
  final String numeroUnidade;
  final bool sucesso;
  final double? valorM3;
  final int? valorLitros;
  final double? confiancaIa;
  final String origem;
  final String status;
  final bool suspeitaVazamento;
  final bool permiteRecurso;
  final String? motivo;

  const LeituraModel({
    required this.id,
    required this.unidadeId,
    required this.numeroUnidade,
    required this.sucesso,
    this.valorM3,
    this.valorLitros,
    this.confiancaIa,
    required this.origem,
    required this.status,
    required this.suspeitaVazamento,
    required this.permiteRecurso,
    this.motivo,
  });

  factory LeituraModel.fromJson(Map<String, dynamic> json) => LeituraModel(
        id: json['id'],
        unidadeId: json['unidadeId'],
        numeroUnidade: json['numeroUnidade'],
        sucesso: json['sucesso'],
        valorM3: (json['valorM3'] as num?)?.toDouble(),
        valorLitros: json['valorLitros'],
        confiancaIa: (json['confiancaIa'] as num?)?.toDouble(),
        origem: json['origem'] ?? 'Ia',
        status: json['status'],
        suspeitaVazamento: json['suspeitaVazamento'] ?? false,
        permiteRecurso: json['permiteRecurso'] ?? false,
        motivo: json['motivo'],
      );

  String get valorFormatado => valorM3 != null ? '${valorM3!.toStringAsFixed(0)} m³' : '—';
  int get confiancaPercent => ((confiancaIa ?? 0) * 100).round();
}
