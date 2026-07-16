---
name: Academic Precision
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#424750'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#737781'
  outline-variant: '#c3c6d1'
  surface-tint: '#335f99'
  primary: '#003466'
  on-primary: '#ffffff'
  primary-container: '#1a4b84'
  on-primary-container: '#93bcfc'
  inverse-primary: '#a6c8ff'
  secondary: '#006c49'
  on-secondary: '#ffffff'
  secondary-container: '#6cf8bb'
  on-secondary-container: '#00714d'
  tertiary: '#4c2d00'
  on-tertiary: '#ffffff'
  tertiary-container: '#6a4200'
  on-tertiary-container: '#ffa825'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#d5e3ff'
  primary-fixed-dim: '#a6c8ff'
  on-primary-fixed: '#001c3b'
  on-primary-fixed-variant: '#144780'
  secondary-fixed: '#6ffbbe'
  secondary-fixed-dim: '#4edea3'
  on-secondary-fixed: '#002113'
  on-secondary-fixed-variant: '#005236'
  tertiary-fixed: '#ffddb8'
  tertiary-fixed-dim: '#ffb95f'
  on-tertiary-fixed: '#2a1700'
  on-tertiary-fixed-variant: '#653e00'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  headline-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg-mobile:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.05em
  label-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 4px
  xs: 8px
  sm: 16px
  md: 24px
  lg: 32px
  xl: 48px
  container-max: 1440px
  sidebar-width: 260px
---

## Brand & Style

Hệ thống thiết kế này tập trung vào sự tin cậy, hiệu quả và tính minh bạch trong giáo dục. Mục tiêu là tạo ra một môi trường học tập kỹ thuật số ít gây xao nhãng, giúp người dùng (giáo viên, học sinh, quản trị viên) tập trung hoàn toàn vào nội dung đào tạo.

Phong cách chủ đạo là **Corporate Modern** kết hợp với **Minimalism**. Sự kết hợp này mang lại cảm giác chuyên nghiệp của một tổ chức giáo dục cao cấp nhưng vẫn đảm bảo tính linh hoạt, dễ tiếp cận của một ứng dụng SaaS hiện đại. Giao diện ưu tiên khoảng trắng rộng rãi, các đường nét sắc sảo và hệ thống phân cấp thông tin rõ ràng thông qua việc sử dụng thẻ (cards) và bảng biểu (tables) có cấu trúc chặt chẽ.

## Colors

Bảng màu được xây dựng dựa trên tâm lý học màu sắc trong giáo dục:
- **Primary (Education Blue - #1A4B84):** Màu xanh dương đậm đại diện cho trí tuệ, sự ổn định và tính chuyên nghiệp. Đây là màu chủ đạo cho các hành chính, thanh điều hướng và nhận diện thương hiệu.
- **Secondary (Success Green - #10B981):** Sử dụng cho các chỉ số hoàn thành, điểm số đạt và các trạng thái tích cực.
- **Tertiary (Warning Amber - #F59E0B):** Dành cho các nhắc nhở hạn chót, cảnh báo hoặc các mục cần lưu ý ngay lập tức.
- **Neutral (Slate Gray - #64748B):** Hệ màu xám trung tính giúp cân bằng thị giác, sử dụng cho văn bản phụ và các đường kẻ phân cách để giữ cho giao diện luôn sạch sẽ.

## Typography

Sử dụng **Inter** làm phông chữ duy nhất để đảm bảo tính nhất quán tối đa và khả năng đọc tuyệt vời trên mọi độ phân giải màn hình. 
- Hệ thống phân cấp sử dụng trọng lượng chữ (weight) để phân biệt: `Bold` (700) cho tiêu đề lớn, `Semi-Bold` (600) cho tiêu đề phần, và `Medium` (500) cho các nhãn (labels). 
- Khoảng cách dòng (line-height) được thiết lập rộng rãi (từ 1.4 đến 1.5 lần kích thước chữ) để giảm mỏi mắt khi đọc các bài học dài hoặc dữ liệu bảng biểu phức tạp.

## Layout & Spacing

Hệ thống sử dụng mô hình **Fluid Grid** linh hoạt kết hợp với cấu trúc Sidebar cố định bên trái.
- **Desktop:** Sử dụng lưới 12 cột với gutter 24px và margin 32px. Thanh điều hướng bên trái (Sidebar) cố định ở 260px để tối ưu không gian hiển thị nội dung học tập ở trung tâm.
- **Tablet:** Sidebar có thể thu gọn thành dạng icon (64px), lưới chuyển sang 8 cột.
- **Mobile:** Chuyển sang bố cục 1 cột, Sidebar chuyển thành dạng menu Drawer (vuốt từ cạnh). Margin an toàn giảm xuống còn 16px.
- **Spacing Rhythm:** Mọi khoảng cách đều dựa trên bội số của 4px (Base 4) để tạo ra sự cân bằng hoàn hảo về mặt thị giác.

## Elevation & Depth

Trong thiết kế này, độ sâu được tạo ra bằng phương pháp **Tonal Layers** kết hợp với **Low-contrast outlines** thay vì sử dụng bóng đổ đậm.
- **Lớp nền (Background):** Sử dụng màu xám cực nhạt (#F8FAFC) để tạo sự tách biệt với các thẻ nội dung.
- **Thẻ (Cards):** Sử dụng nền trắng thuần (#FFFFFF) với đường viền mảnh 1px màu xám nhạt (#E2E8F0).
- **Trạng thái nổi (Hover/Active):** Khi tương tác, sử dụng bóng đổ cực nhẹ, khuếch tán rộng (Blur 15px, Opacity 5%) để tạo cảm giác phần tử đang nổi lên mà không làm mất đi vẻ sạch sẽ của tổng thể.
- **Sidebar & Header:** Có mức phân cấp cao nhất, sử dụng Border-right hoặc Border-bottom để định vị không gian thay vì bóng đổ.

## Shapes

Bo góc cấp độ **Rounded** (0.5rem / 8px) được chọn làm tiêu chuẩn cho hầu hết các yếu tố giao diện.
- **Nút & Input:** Bo góc 8px tạo cảm giác hiện đại và thân thiện nhưng vẫn giữ được sự chuyên nghiệp cần thiết cho một hệ thống quản lý.
- **Thẻ (Cards):** Sử dụng `rounded-lg` (16px) để tạo sự phân tách rõ rệt giữa các khối nội dung lớn trên Dashboard.
- **Hệ thống nhãn (Badges/Chips):** Sử dụng dạng `Pill-shaped` (đường cong tối đa) để dễ dàng phân biệt với các nút hành động chính.

## Components

Hệ thống thành phần được thiết kế để tối ưu hóa việc quản lý dữ liệu và trải nghiệm học tập:

- **Buttons:** Nút Primary sử dụng màu Education Blue với chữ trắng. Nút Secondary sử dụng outline mỏng. Trạng thái `disabled` sử dụng màu xám nhạt để tránh nhầm lẫn.
- **Dashboard Cards:** Các thẻ thống kê nhanh (như số học sinh, điểm trung bình) bao gồm một icon màu nền nhạt, một tiêu đề nhỏ và số liệu lớn nổi bật.
- **Tables:** Bảng dữ liệu không sử dụng đường kẻ dọc, chỉ sử dụng đường kẻ ngang mỏng. Hàng tiêu đề có nền xám nhạt và chữ in hoa (label-md). Các hàng có hiệu ứng hover đổi màu nền nhẹ để người dùng dễ theo dõi dòng dữ liệu.
- **Charts:** Sử dụng biểu đồ đường (line charts) hoặc biểu đồ cột (bar charts) với các tông màu Primary và Secondary. Đường nét biểu đồ được làm mượt (curved) để trông hiện đại hơn.
- **Inputs:** Ô nhập liệu có nhãn nằm phía trên, border màu xám nhạt. Khi focus, border chuyển sang màu Primary với một lớp bóng mờ (ring) bao quanh.
- **Navigation Sidebar:** Các mục menu được nhóm theo chức năng (Học tập, Quản lý, Báo cáo). Mục đang hoạt động (Active) có nền xanh nhạt và một đường kẻ dọc (indicator) ở cạnh trái.
- **Progress Bars:** Thanh tiến trình học tập sử dụng màu Secondary trên nền xám nhạt, thể hiện rõ ràng % hoàn thành khóa học.