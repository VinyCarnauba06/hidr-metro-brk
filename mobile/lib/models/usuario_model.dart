class UsuarioModel {
  final String token;
  final String nome;
  final String perfil;
  final DateTime expiraEm;

  const UsuarioModel({
    required this.token,
    required this.nome,
    required this.perfil,
    required this.expiraEm,
  });

  factory UsuarioModel.fromJson(Map<String, dynamic> json) => UsuarioModel(
        token: json['token'],
        nome: json['nome'],
        perfil: json['perfil'],
        expiraEm: DateTime.parse(json['expiraEm']),
      );

  bool get tokenValido => DateTime.now().isBefore(expiraEm);
}
