class OrdemServicoModel {
  final int id;
  final int mes;
  final int ano;
  final String condominio;
  final String? endereco;
  final int totalUnidades;
  final String status;
  final DateTime? dataLimite;

  const OrdemServicoModel({
    required this.id,
    required this.mes,
    required this.ano,
    required this.condominio,
    this.endereco,
    required this.totalUnidades,
    required this.status,
    this.dataLimite,
  });

  factory OrdemServicoModel.fromJson(Map<String, dynamic> json) => OrdemServicoModel(
        id: json['id'],
        mes: json['mes'],
        ano: json['ano'],
        condominio: json['condominio'],
        endereco: json['endereco'],
        totalUnidades: json['totalUnidades'],
        status: json['status'],
        dataLimite: json['dataLimite'] != null ? DateTime.parse(json['dataLimite']) : null,
      );

  String get competencia => '${mes.toString().padLeft(2, '0')}/$ano';

  // Não tem problema ir antes do prazo — o problema é passar dele sem ter ido.
  bool get prazoVencido => dataLimite != null && DateTime.now().isAfter(dataLimite!);
}
