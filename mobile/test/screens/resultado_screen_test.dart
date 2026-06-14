// mobile/test/screens/resultado_screen_test.dart
// Widget test: ResultadoScreen exibe alerta de vazamento quando suspeitaVazamento = true
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hidrometro_brk/models/leitura_model.dart';
import 'package:hidrometro_brk/models/ordem_servico_model.dart';
import 'package:hidrometro_brk/screens/resultado_screen.dart';

void main() {
  final osTest = OrdemServicoModel(
    id: 1,
    mes: 6,
    ano: 2026,
    condominio: 'Condo Teste',
    totalUnidades: 10,
    status: 'EmProgresso',
  );

  group('ResultadoScreen', () {
    testWidgets('exibe alerta de vazamento quando suspeitaVazamento = true', (tester) async {
      final leituraComVazamento = LeituraModel(
        id: 1,
        unidadeId: 1,
        numeroUnidade: '101',
        sucesso: true,
        valorM3: 1500,
        valorLitros: 0,
        confiancaIa: 0.95,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: true,
        permiteRecurso: false,
      );

      bool proximaChamada = false;

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leituraComVazamento,
            os: osTest,
            onProxima: () => proximaChamada = true,
          ),
        ),
      );

      // Verifica que mostra o alerta de vazamento
      expect(find.textContaining('Suspeita de vazamento'), findsOneWidget);
      expect(find.textContaining('operador será notificado'), findsOneWidget);
      expect(find.byIcon(Icons.water_drop), findsOneWidget);

      // Verifica que mostra "Leitura Registrada!" (sucesso)
      expect(find.text('Leitura Registrada!'), findsOneWidget);
    });

    testWidgets('NÃO exibe alerta de vazamento quando suspeitaVazamento = false', (tester) async {
      final leituraNormal = LeituraModel(
        id: 2,
        unidadeId: 2,
        numeroUnidade: '102',
        sucesso: true,
        valorM3: 200,
        valorLitros: 0,
        confiancaIa: 0.98,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: false,
        permiteRecurso: false,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leituraNormal,
            os: osTest,
            onProxima: () {},
          ),
        ),
      );

      // Não deve mostrar alerta de vazamento
      expect(find.textContaining('Suspeita de vazamento'), findsNothing);
      expect(find.byIcon(Icons.water_drop), findsNothing);

      // Mas deve mostrar sucesso
      expect(find.text('Leitura Registrada!'), findsOneWidget);
    });

    testWidgets('exibe valor lido e confiança corretamente', (tester) async {
      final leitura = LeituraModel(
        id: 3,
        unidadeId: 3,
        numeroUnidade: '103',
        sucesso: true,
        valorM3: 1234,
        valorLitros: 567,
        confiancaIa: 0.92,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: false,
        permiteRecurso: false,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leitura,
            os: osTest,
            onProxima: () {},
          ),
        ),
      );

      // Valor formatado: "1234 m³"
      expect(find.text('1234 m³'), findsOneWidget);
      // Confiança: 92%
      expect(find.text('92%'), findsOneWidget);
      // Unidade
      expect(find.text('Unidade 103'), findsOneWidget);
    });

    testWidgets('mostra botão de recurso manual quando foto ilegível e permiteRecurso = true', (tester) async {
      final leituraIlegivel = LeituraModel(
        id: 4,
        unidadeId: 4,
        numeroUnidade: '104',
        sucesso: false,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: false,
        permiteRecurso: true,
        motivo: 'Não foi possível extrair a leitura',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leituraIlegivel,
            os: osTest,
            onProxima: () {},
          ),
        ),
      );

      // Mostra "Foto Ilegível"
      expect(find.text('Foto Ilegível'), findsOneWidget);

      // Mostra botão de recurso manual
      expect(find.text('Digitar Manualmente'), findsOneWidget);
      expect(find.byIcon(Icons.edit), findsOneWidget);
    });

    testWidgets('NÃO mostra botão de recurso manual quando permiteRecurso = false', (tester) async {
      final leituraIlegivelSemRecurso = LeituraModel(
        id: 5,
        unidadeId: 5,
        numeroUnidade: '105',
        sucesso: false,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: false,
        permiteRecurso: false,
        motivo: 'Foto muito escura',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leituraIlegivelSemRecurso,
            os: osTest,
            onProxima: () {},
          ),
        ),
      );

      expect(find.text('Foto Ilegível'), findsOneWidget);
      expect(find.text('Digitar Manualmente'), findsNothing);
    });

    testWidgets('botão Próxima Unidade chama callback onProxima', (tester) async {
      bool chamou = false;
      final leitura = LeituraModel(
        id: 6,
        unidadeId: 6,
        numeroUnidade: '106',
        sucesso: true,
        valorM3: 500,
        confiancaIa: 0.99,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: false,
        permiteRecurso: false,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leitura,
            os: osTest,
            onProxima: () => chamou = true,
          ),
        ),
      );

      await tester.tap(find.text('Próxima Unidade'));
      expect(chamou, isTrue);
    });

    testWidgets('abre dialog de recurso manual ao tocar no botão', (tester) async {
      final leitura = LeituraModel(
        id: 7,
        unidadeId: 7,
        numeroUnidade: '107',
        sucesso: false,
        origem: 'Ia',
        status: 'Pendente',
        suspeitaVazamento: false,
        permiteRecurso: true,
        motivo: 'Imagem borrada',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: ResultadoScreen(
            leitura: leitura,
            os: osTest,
            onProxima: () {},
          ),
        ),
      );

      // Toca no botão "Digitar Manualmente"
      await tester.tap(find.text('Digitar Manualmente'));
      await tester.pumpAndSettle();

      // O BottomSheet deve abrir com os campos
      expect(find.text('Digitar Leitura Manual'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Metros cúbicos (m³)'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Litros'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Observação (opcional)'), findsOneWidget);
      expect(find.text('Cancelar'), findsOneWidget);
      expect(find.text('Salvar'), findsOneWidget);
    });
  });
}
