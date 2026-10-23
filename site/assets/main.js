// BinDay - landing page
// No frameworks, no build step. One stylesheet, system fonts, dark by default
// with a light mode for people who prefer it.

// Wait for DOM to be ready
document.addEventListener('DOMContentLoaded', function() {
  // Mobile menu toggle
  const mobileMenuBtn = document.querySelector('.mobile-menu-btn');
  const mobileMenu = document.querySelector('.mobile-menu');
  
  if (mobileMenuBtn && mobileMenu) {
    mobileMenuBtn.addEventListener('click', function() {
      mobileMenu.classList.toggle('active');
    });
  }
  
  // Desktop navigation hover effects
  const navItems = document.querySelectorAll('.nav a');
  navItems.forEach(item => {
    item.addEventListener('mouseenter', function() {
      this.style.color = getComputedStyle(document.documentElement).getPropertyValue('--accent');
    });
    
    item.addEventListener('mouseleave', function() {
      this.style.color = '';
    });
  });
  
  // Download button
  const downloadBtn = document.querySelector('.btn-primary');
  if (downloadBtn) {
    downloadBtn.addEventListener('click', function(e) {
      e.preventDefault();
      window.location.href = 'https://github.com/khansarim007/binday/releases/latest/download/BinDay.exe';
    });
  }
  
  // View source button
  const viewSourceBtn = document.querySelector('.btn-ghost');
  if (viewSourceBtn) {
    viewSourceBtn.addEventListener('click', function(e) {
      e.preventDefault();
      window.location.href = 'https://github.com/khansarim007/binday';
    });
  }
  
  // Smooth scrolling for anchor links
  document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function(e) {
      e.preventDefault();
      const targetId = this.getAttribute('href').substring(1);
      const targetElement = document.getElementById(targetId);
      
      if (targetElement) {
        window.scrollTo({
          top: targetElement.offsetTop - 80,
          behavior: 'smooth'
        });
      }
    });
  });
  
  // Smooth scroll for internal links
  document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function(e) {
      e.preventDefault();
      const targetId = this.getAttribute('href').substring(1);
      const targetElement = document.getElementById(targetId);
      
      if (targetElement) {
        window.scrollTo({
          top: targetElement.offsetTop - 80,
          behavior: 'smooth'
        });
      }
    });
  });