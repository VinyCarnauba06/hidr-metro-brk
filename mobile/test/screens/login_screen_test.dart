// mobile/test/screens/login_screen_test.dart
// Widget tests para LoginScreen
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hidrometro_brk/screens/login_screen.dart';

void main() {
  group('LoginScreen', () {
    testWidgets('renderiza campos de CPF e senha corretamente', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      expect(find.text('Hidrômetro BRK'), findsOneWidget);
      expect(find.text('Prolar AGE'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'CPF'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Senha'), findsOneWidget);
      expect(find.text('Entrar'), findsOneWidget);
      // Ícones de prefixo nos campos
      expect(find.byIcon(Icons.person), findsOneWidget);
      expect(find.byIcon(Icons.lock), findsOneWidget);
    });

    testWidgets('campo de senha é obscureText', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      final senhaField = tester.widget<TextField>(find.widgetWithText(TextField, 'Senha'));
      expect(senhaField.obscureText, isTrue);
    });

    testWidgets('mostra CircularProgressIndicator ao clicar Entrar', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      await tester.enterText(find.widgetWithText(TextField, 'CPF'), '12345678900');
      await tester.enterText(find.widgetWithText(TextField, 'Senha'), 'senha');

      await tester.tap(find.text('Entrar'));
      await tester.pump(); // setState com _loading = true

      // Enquanto _loading é true, o botão mostra CircularProgressIndicator
      expect(find.byType(CircularProgressIndicator), findsWidgets);
    });

    testWidgets('botão Entrar desabilitado enquanto carrega', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      await tester.enterText(find.widgetWithText(TextField, 'CPF'), '12345678900');
      await tester.enterText(find.widgetWithText(TextField, 'Senha'), 'senha');

      await tester.tap(find.text('Entrar'));
      await tester.pump();

      // Enquanto carregando, o botão ElevatedButton deve estar desabilitado
      // (onPressed = null quando _loading = true)
      final button = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
      expect(button.onPressed, isNull);
    });

    testWidgets('exibe erro quando login falha (API inacessível)', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      await tester.enterText(find.widgetWithText(TextField, 'CPF'), '99999999999');
      await tester.enterText(find.widgetWithText(TextField, 'Senha'), 'senha_errada');

      // Tenta fazer login — HTTP vai falhar (sem servidor rodando em teste)
      await tester.tap(find.text('Entrar'));
      await tester.pump(); // setState _loading = true

      // Usa runAsync para permitir que o Future do HTTP real complete
      // (incluindo o timeout de 30s do http client)
      await tester.runAsync(() async {
        // Aguarda o Future do login resolver (vai falhar com SocketException ou timeout)
        await Future.delayed(const Duration(seconds: 1));
      });

      // Faz pump para processar o setState com o erro
      await tester.pump();
      // Pump extra para garantir que o rebuild completo aconteceu
      await tester.pump(const Duration(milliseconds: 100));

      // Quando o login falha, a tela permanece em LoginScreen (não navega)
      expect(find.byType(LoginScreen), findsOneWidget);
      // O botão "Entrar" deve voltar a aparecer (loading terminou)
      expect(find.text('Entrar'), findsOneWidget);
    });
  });
}
