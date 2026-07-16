// mobile/test/screens/login_screen_test.dart
// Widget tests para LoginScreen
import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:hidrometro_brk/screens/login_screen.dart';
import 'package:hidrometro_brk/services/auth_service.dart';

void main() {
  final originalClient = AuthService.client;
  tearDown(() => AuthService.client = originalClient);

  group('LoginScreen', () {
    testWidgets('renderiza campos de Email e senha corretamente', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      expect(find.text('Hidrômetro BRK'), findsOneWidget);
      expect(find.text('Prolar AGE'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Email'), findsOneWidget);
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
      final respostaPendente = Completer<http.Response>();
      AuthService.client = MockClient((request) => respostaPendente.future);

      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'fiscal@prolar.com');
      await tester.enterText(find.widgetWithText(TextField, 'Senha'), 'senha');

      await tester.tap(find.text('Entrar'));
      await tester.pump(); // setState com _loading = true; a requisição HTTP ainda não respondeu

      // Enquanto _loading é true, o botão mostra CircularProgressIndicator
      expect(find.byType(CircularProgressIndicator), findsWidgets);

      respostaPendente.complete(http.Response('{"message":"erro"}', 400));
      await tester.pumpAndSettle();
    });

    testWidgets('botão Entrar desabilitado enquanto carrega', (tester) async {
      final respostaPendente = Completer<http.Response>();
      AuthService.client = MockClient((request) => respostaPendente.future);

      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'fiscal@prolar.com');
      await tester.enterText(find.widgetWithText(TextField, 'Senha'), 'senha');

      await tester.tap(find.text('Entrar'));
      await tester.pump();

      // Enquanto carregando, o botão ElevatedButton deve estar desabilitado
      // (onPressed = null quando _loading = true)
      final button = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
      expect(button.onPressed, isNull);

      respostaPendente.complete(http.Response('{"message":"erro"}', 400));
      await tester.pumpAndSettle();
    });

    testWidgets('exibe erro quando login falha (API inacessível)', (tester) async {
      AuthService.client = MockClient((request) async => http.Response('{"message":"erro"}', 401));

      await tester.pumpWidget(
        const MaterialApp(home: LoginScreen()),
      );

      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'desconhecido@prolar.com');
      await tester.enterText(find.widgetWithText(TextField, 'Senha'), 'senha_errada');

      await tester.tap(find.text('Entrar'));
      await tester.pumpAndSettle();

      // Quando o login falha, a tela permanece em LoginScreen (não navega)
      expect(find.byType(LoginScreen), findsOneWidget);
      // O botão "Entrar" deve voltar a aparecer (loading terminou)
      expect(find.text('Entrar'), findsOneWidget);
    });
  });
}
