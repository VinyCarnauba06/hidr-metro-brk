class OrdemServicoModel {
  final int id;
  final int mes;
  final int ano;
  final String condominio;
  final String? endereco;
  final int totalUnidades;
  final String status;

  const OrdemServicoModel({
    required this.id,
    required this.mes,
    required this.ano,
    required this.condominio,
    this.endereco,
    required this.totalUnidades,
    required this.status,
  });

  factory OrdemServicoModel.fromJson(Map<String, dynamic> json) => OrdemServicoModel(
        id: json['id'],
        mes: json['mes'],
        ano: json['ano'],
        condominio: json['condominio'],
        endereco: json['endereco'],
        totalUnidades: json['totalUnidades'],
        status: json['status'],
      );

  String get competencia => '${mes.toString().padLeft(2, '0')}/$ano';
}
