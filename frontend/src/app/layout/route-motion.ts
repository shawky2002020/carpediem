// Animate only the incoming view. Browser back, focus and scroll remain synchronous.
export class RouteMotion {
  private current?: Animation;
  private frame = 0;

  enter(main: HTMLElement, previous: string, next: string): void {
    cancelAnimationFrame(this.frame);
    this.current?.cancel();
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    const depth = (path: string) => path.split('/').filter(Boolean).length;
    const direction = Math.sign(depth(next) - depth(previous));
    this.frame = requestAnimationFrame(() => {
      const view = main.querySelector<HTMLElement>('router-outlet + *');
      if (!view || !view.animate) return;
      const style = getComputedStyle(main);
      const distance = style.getPropertyValue('--cd-travel').trim();
      this.current = view.animate([
        { opacity: .88, transform: direction ? `translateX(calc(${direction} * ${distance}))` : 'none' },
        { opacity: 1, transform: 'none' },
      ], { duration: parseFloat(style.getPropertyValue('--cd-duration-normal')), easing: style.getPropertyValue('--cd-ease-standard').trim() });
    });
  }
}
