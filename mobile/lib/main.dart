import 'package:flutter/material.dart';
import 'screens/login_screen.dart';

void main() {
  runApp(const HidrometroApp());
}

class HidrometroApp extends StatelessWidget {
  const HidrometroApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Hidrômetro BRK',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF1D4ED8)),
        useMaterial3: true,
        fontFamily: 'DM Sans',
      ),
      home: const LoginScreen(),
    );
  }
}
