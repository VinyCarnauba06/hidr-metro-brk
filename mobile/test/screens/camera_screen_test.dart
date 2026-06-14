// mobile/test/screens/camera_screen_test.dart
// Widget test: CameraScreen mostra progress bar com valor correto
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hidrometro_brk/models/ordem_servico_model.dart';
import 'package:hidrometro_brk/screens/camera_screen.dart';

void main() {
  final osTest = OrdemServicoModel(
    id: 1,
    mes: 6,
    ano: 2026,
    condominio: 'Condo Teste',
    endereco: 'Rua ABC, 123',
    totalUnidades: 10,
    status: 'EmProgresso',
  );

  group('CameraScreen', () {
    testWidgets('renderiza com nome do condomínio no AppBar', (tester) async {
      await tester.pumpWidget(
        MaterialApp(home: CameraScreen(os: osTest)),
      );
      // pump() sem settle — há um Future HTTP pendente que vai expirar
      // mas o widget já renderizou o estado inicial
      await tester.pump();

      expect(find.text('Condo Teste'), findsOneWidget);
    });

    testWidgets('mostra progress bar com valores iniciais do OS', (tester) async {
      await tester.pumpWidget(
        MaterialApp(home: CameraScreen(os: osTest)),
      );
      await tester.pump();

      // LinearProgressIndicator deve existir
      expect(find.byType(LinearProgressIndicator), findsOneWidget);

      // Antes do progresso carregar da API, mostra valores default:
      // registradas = _progresso?['leiturasRegistradas'] ?? 0 = 0
      // total = _progresso?['totalUnidades'] ?? widget.os.totalUnidades = 10
      expect(find.text('0 / 10 leituras'), findsOneWidget);
      expect(find.text('0%'), findsOneWidget);
      expect(find.text('10 faltando'), findsOneWidget);
    });

    testWidgets('mostra botão de fotografar habilitado', (tester) async {
      await tester.pumpWidget(
        MaterialApp(home: CameraScreen(os: osTest)),
      );
      await tester.pump();

      expect(find.text('Fotografar Hidrômetro'), findsOneWidget);
      // Ícone camera_alt aparece no body (grande) e no botão
      expect(find.byIcon(Icons.camera_alt), findsWidgets);
    });

    testWidgets('mostra instruções de uso para o fiscal', (tester) async {
      await tester.pumpWidget(
        MaterialApp(home: CameraScreen(os: osTest)),
      );
      await tester.pump();

      expect(find.text('Aponte a câmera para o hidrômetro'), findsOneWidget);
      expect(find.textContaining('boa iluminação'), findsOneWidget);
    });

    testWidgets('LinearProgressIndicator tem valor 0.0 quando sem progresso', (tester) async {
      await tester.pumpWidget(
        MaterialApp(home: CameraScreen(os: osTest)),
      );
      await tester.pump();

      final progressBar = tester.widget<LinearProgressIndicator>(
        find.byType(LinearProgressIndicator),
      );
      // Sem progresso carregado, pct = 0/10 = 0.0
      expect(progressBar.value, equals(0.0));
    });
  });
}
