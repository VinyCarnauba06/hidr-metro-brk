import 'package:flutter/material.dart';

class FramingOverlay extends StatefulWidget {
  final bool isBlurry;

  const FramingOverlay({super.key, this.isBlurry = false});

  @override
  State<FramingOverlay> createState() => _FramingOverlayState();
}

class _FramingOverlayState extends State<FramingOverlay> with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _opacity;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: const Duration(milliseconds: 700))
      ..repeat(reverse: true);
    _opacity = Tween<double>(begin: 0.5, end: 1.0).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeInOut),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final w = constraints.maxWidth * 0.7;
        final h = constraints.maxHeight * 0.45;
        final left = (constraints.maxWidth - w) / 2;
        final top = (constraints.maxHeight - h) / 2 - 30;

        return AnimatedBuilder(
          animation: _opacity,
          builder: (context, _) {
            final borderColor = widget.isBlurry
                ? Colors.orange.withOpacity(_opacity.value)
                : Colors.white.withOpacity(0.85);

            return Stack(
              children: [
                // Dimmed overlay outside the frame
                Positioned.fill(
                  child: CustomPaint(
                    painter: _DimPainter(
                      frameRect: Rect.fromLTWH(left, top, w, h),
                      radius: 12,
                    ),
                  ),
                ),
                // Frame border
                Positioned(
                  left: left,
                  top: top,
                  width: w,
                  height: h,
                  child: Container(
                    decoration: BoxDecoration(
                      border: Border.all(color: borderColor, width: 2.5),
                      borderRadius: BorderRadius.circular(12),
                    ),
                  ),
                ),
                // Corner accents
                ..._corners(left, top, w, h, borderColor),
                // Label below frame
                Positioned(
                  left: left,
                  top: top + h + 12,
                  width: w,
                  child: Text(
                    widget.isBlurry ? 'Estabilize a câmera' : 'Centralize o mostrador aqui',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: widget.isBlurry ? Colors.orange : Colors.white,
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      shadows: const [Shadow(blurRadius: 4, color: Colors.black54)],
                    ),
                  ),
                ),
              ],
            );
          },
        );
      },
    );
  }

  List<Widget> _corners(double left, double top, double w, double h, Color color) {
    const size = 20.0;
    const thickness = 3.0;
    final r = BorderRadius.circular(3);

    Widget corner(double l, double t, bool flipX, bool flipY) {
      return Positioned(
        left: l,
        top: t,
        child: Transform.scale(
          scaleX: flipX ? -1 : 1,
          scaleY: flipY ? -1 : 1,
          child: SizedBox(
            width: size,
            height: size,
            child: CustomPaint(painter: _CornerPainter(color: color, thickness: thickness)),
          ),
        ),
      );
    }

    return [
      corner(left, top, false, false),
      corner(left + w - size, top, true, false),
      corner(left, top + h - size, false, true),
      corner(left + w - size, top + h - size, true, true),
    ];
  }
}

class _DimPainter extends CustomPainter {
  final Rect frameRect;
  final double radius;

  const _DimPainter({required this.frameRect, required this.radius});

  @override
  void paint(Canvas canvas, Size size) {
    final fullRect = Rect.fromLTWH(0, 0, size.width, size.height);
    final paint = Paint()..color = Colors.black54;

    final path = Path()
      ..addRect(fullRect)
      ..addRRect(RRect.fromRectAndRadius(frameRect, Radius.circular(radius)))
      ..fillType = PathFillType.evenOdd;

    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(_DimPainter old) => old.frameRect != frameRect;
}

class _CornerPainter extends CustomPainter {
  final Color color;
  final double thickness;

  const _CornerPainter({required this.color, required this.thickness});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = thickness
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;

    canvas.drawLine(Offset(0, size.height), Offset.zero, paint);
    canvas.drawLine(Offset.zero, Offset(size.width, 0), paint);
  }

  @override
  bool shouldRepaint(_CornerPainter old) => old.color != color;
}
