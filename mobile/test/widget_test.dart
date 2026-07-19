import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:hidrometro_brk/main.dart';

void main() {
  testWidgets('HidrometroApp renderiza a tela de login', (WidgetTester tester) async {
    await tester.pumpWidget(const HidrometroApp());

    expect(find.text('HIDRO'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
    expect(find.byType(TextField), findsNWidgets(2));
  });
}
